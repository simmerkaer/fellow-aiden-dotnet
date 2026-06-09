using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FellowAiden;

/// <summary>
/// Client for the Fellow Aiden coffee brewer cloud API.
/// <para>
/// Because authentication is asynchronous, create instances with the static
/// <see cref="CreateAsync"/> factory:
/// </para>
/// <code>
/// await using var aiden = await FellowAidenClient.CreateAsync(
///     new FellowAidenOptions { Email = email, Password = password });
/// </code>
/// </summary>
public sealed class FellowAidenClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Regex BrewLinkPattern = new(@"(?:.*?/p/)?([a-zA-Z0-9]+)/?$", RegexOptions.Compiled);

    private readonly FellowAidenOptions _options;
    private readonly string _baseUrl;
    private readonly HttpClient _http;

    private string? _token;
    private DeviceConfig? _deviceConfig;
    private string? _brewerId;
    private IReadOnlyList<Profile>? _profiles;
    private IReadOnlyList<Schedule>? _schedules;

    /// <summary>Creates a client without authenticating. Prefer <see cref="CreateAsync"/>.</summary>
    /// <param name="options">Client configuration.</param>
    /// <param name="innerHandler">Optional inner handler (inject a stub in tests). Defaults to a new <see cref="HttpClientHandler"/>.</param>
    public FellowAidenClient(FellowAidenOptions options, HttpMessageHandler? innerHandler = null)
    {
        if (string.IsNullOrEmpty(options.Email) || string.IsNullOrEmpty(options.Password))
        {
            throw new FellowAidenException("Both email and password are required.");
        }

        _options = options;
        _baseUrl = options.BaseUrl.TrimEnd('/');

        var handler = new RetryingHandler(
            getToken: () => _token,
            reauthenticate: LoginAsync,
            userAgent: options.UserAgent,
            maxRetries: options.MaxRetries)
        {
            InnerHandler = innerHandler ?? new HttpClientHandler(),
        };
        _http = new HttpClient(handler);
    }

    /// <summary>The bound device's display name, or null if unset.</summary>
    public string? DisplayName => _deviceConfig?.DisplayName;

    /// <summary>The bound device's id.</summary>
    public string? BrewerId => _brewerId;

    /// <summary>Authenticates and loads the brewer, returning a ready-to-use client.</summary>
    public static async Task<FellowAidenClient> CreateAsync(
        FellowAidenOptions options, HttpMessageHandler? innerHandler = null, CancellationToken cancellationToken = default)
    {
        var client = new FellowAidenClient(options, innerHandler);
        await client.AuthenticateAsync(cancellationToken).ConfigureAwait(false);
        return client;
    }

    /// <summary>Logs in (or re-authenticates) and refreshes the bound device.</summary>
    public async Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        await LoginAsync(cancellationToken).ConfigureAwait(false);
        await LoadDeviceAsync(cancellationToken).ConfigureAwait(false);
    }

    // --- Device ---------------------------------------------------------------

    /// <summary>Returns the cached device config, optionally re-fetching first.</summary>
    public async Task<DeviceConfig> GetDeviceConfigAsync(bool refresh = false, CancellationToken cancellationToken = default)
    {
        if (refresh || _deviceConfig is null)
        {
            await LoadDeviceAsync(cancellationToken).ConfigureAwait(false);
        }

        return _deviceConfig!;
    }

    /// <summary>Applies a single device setting via PATCH. Returns the raw response body.</summary>
    public Task<string> AdjustSettingAsync(string setting, object value, CancellationToken cancellationToken = default)
    {
        var id = BrewerIdOrThrow();
        return SendAsync(HttpMethod.Patch, $"/devices/{id}", new Dictionary<string, object> { [setting] = value }, false, cancellationToken);
    }

    // --- Profiles -------------------------------------------------------------

    /// <summary>All profiles (lazily loaded and cached).</summary>
    public async Task<IReadOnlyList<Profile>> GetProfilesAsync(bool refresh = false, CancellationToken cancellationToken = default)
    {
        var id = BrewerIdOrThrow();
        if (refresh || _profiles is null)
        {
            _profiles = await SendAsync<List<Profile>>(HttpMethod.Get, $"/devices/{id}/profiles", null, cancellationToken).ConfigureAwait(false);
        }

        return _profiles!;
    }

    /// <summary>
    /// Finds a profile by title. Exact (case-insensitive) by default; with
    /// <paramref name="fuzzy"/>, returns the first profile whose similarity ratio exceeds 0.65.
    /// </summary>
    public async Task<Profile?> GetProfileByTitleAsync(string title, bool fuzzy = false, CancellationToken cancellationToken = default)
    {
        var target = title.ToLowerInvariant();
        foreach (var profile in await GetProfilesAsync(false, cancellationToken).ConfigureAwait(false))
        {
            var current = profile.Title.ToLowerInvariant();
            if (fuzzy && Similarity.Ratio(current, target) > 0.65)
            {
                return profile;
            }

            if (current == target)
            {
                return profile;
            }
        }

        return null;
    }

    /// <summary>Creates a new profile after validating it.</summary>
    public async Task<Profile> CreateProfileAsync(CoffeeProfile profile, CancellationToken cancellationToken = default)
    {
        profile.Validate();
        var id = BrewerIdOrThrow();
        var result = await SendAsync<Profile>(HttpMethod.Post, $"/devices/{id}/profiles", profile, cancellationToken).ConfigureAwait(false);
        _profiles = null;
        return result;
    }

    /// <summary>Updates an existing profile by id.</summary>
    public async Task<Profile> UpdateProfileAsync(string profileId, CoffeeProfile profile, CancellationToken cancellationToken = default)
    {
        var id = BrewerIdOrThrow();
        if (!await IsValidProfileIdAsync(profileId, cancellationToken).ConfigureAwait(false))
        {
            throw new FellowAidenException($"Profile does not exist. Valid profiles: {await ProfileIdListAsync(cancellationToken).ConfigureAwait(false)}");
        }

        profile.Validate();
        var result = await SendAsync<Profile>(HttpMethod.Patch, $"/devices/{id}/profiles/{profileId}", profile, cancellationToken).ConfigureAwait(false);
        await LoadDeviceAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    /// <summary>Deletes a profile by id.</summary>
    public async Task DeleteProfileByIdAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var id = BrewerIdOrThrow();
        await SendAsync(HttpMethod.Delete, $"/devices/{id}/profiles/{profileId}", null, false, cancellationToken).ConfigureAwait(false);
        _profiles = null;
    }

    /// <summary>Generates a shareable link for a profile.</summary>
    public async Task<string> GenerateShareLinkAsync(string profileId, CancellationToken cancellationToken = default)
    {
        var id = BrewerIdOrThrow();
        var text = await SendAsync(HttpMethod.Post, $"/devices/{id}/profiles/{profileId}/share", null, false, cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(text);
        if (doc.RootElement.TryGetProperty("link", out var link) && link.GetString() is { } url)
        {
            return url;
        }

        throw new FellowAidenException($"Error generating share link: {text}");
    }

    /// <summary>
    /// Extracts a profile from a shared brew link (or bare id). Server-managed
    /// fields are dropped (they are not part of <see cref="CoffeeProfile"/>).
    /// </summary>
    public async Task<CoffeeProfile> ParseBrewLinkUrlAsync(string link, CancellationToken cancellationToken = default)
    {
        var match = BrewLinkPattern.Match(link);
        if (!match.Success)
        {
            throw new FellowAidenException("Invalid profile URL or ID format.");
        }

        var brewId = match.Groups[1].Value;
        var text = await SendAsync(HttpMethod.Get, $"/shared/{brewId}", null, false, cancellationToken).ConfigureAwait(false);
        try
        {
            var profile = JsonSerializer.Deserialize<CoffeeProfile>(text, Json);
            return profile ?? throw new FellowAidenException($"Failed to fetch profile (ID: {brewId}).");
        }
        catch (JsonException ex)
        {
            throw new FellowAidenException($"Failed to parse shared profile (ID: {brewId}).", ex);
        }
    }

    /// <summary>Imports a shared brew link and creates it as a new profile.</summary>
    public async Task<Profile> CreateProfileFromLinkAsync(string link, CancellationToken cancellationToken = default)
    {
        var profile = await ParseBrewLinkUrlAsync(link, cancellationToken).ConfigureAwait(false);
        return await CreateProfileAsync(profile, cancellationToken).ConfigureAwait(false);
    }

    // --- Schedules ------------------------------------------------------------

    /// <summary>All schedules (lazily loaded and cached).</summary>
    public async Task<IReadOnlyList<Schedule>> GetSchedulesAsync(bool refresh = false, CancellationToken cancellationToken = default)
    {
        var id = BrewerIdOrThrow();
        if (refresh || _schedules is null)
        {
            _schedules = await SendAsync<List<Schedule>>(HttpMethod.Get, $"/devices/{id}/schedules", null, cancellationToken).ConfigureAwait(false);
        }

        return _schedules!;
    }

    /// <summary>Creates a new schedule after validating it.</summary>
    public async Task<Schedule> CreateScheduleAsync(CoffeeSchedule schedule, CancellationToken cancellationToken = default)
    {
        schedule.Validate();
        var id = BrewerIdOrThrow();
        var result = await SendAsync<Schedule>(HttpMethod.Post, $"/devices/{id}/schedules", schedule, cancellationToken).ConfigureAwait(false);
        _schedules = null;
        return result;
    }

    /// <summary>Enables or disables a schedule. Returns the raw response body.</summary>
    public async Task<string> ToggleScheduleAsync(string scheduleId, bool enabled, CancellationToken cancellationToken = default)
    {
        var id = BrewerIdOrThrow();
        if (!await IsValidScheduleIdAsync(scheduleId, cancellationToken).ConfigureAwait(false))
        {
            throw new FellowAidenException($"Schedule does not exist. Valid schedules: {await ScheduleIdListAsync(cancellationToken).ConfigureAwait(false)}");
        }

        var text = await SendAsync(HttpMethod.Patch, $"/devices/{id}/schedules/{scheduleId}", new Dictionary<string, object> { ["enabled"] = enabled }, false, cancellationToken).ConfigureAwait(false);
        _schedules = null;
        return text;
    }

    /// <summary>Deletes a schedule by id.</summary>
    public async Task DeleteScheduleByIdAsync(string scheduleId, CancellationToken cancellationToken = default)
    {
        var id = BrewerIdOrThrow();
        if (!await IsValidScheduleIdAsync(scheduleId, cancellationToken).ConfigureAwait(false))
        {
            throw new FellowAidenException($"Schedule does not exist. Valid schedules: {await ScheduleIdListAsync(cancellationToken).ConfigureAwait(false)}");
        }

        await SendAsync(HttpMethod.Delete, $"/devices/{id}/schedules/{scheduleId}", null, false, cancellationToken).ConfigureAwait(false);
        _schedules = null;
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    // --- Internals ------------------------------------------------------------

    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        string text;
        try
        {
            text = await SendAsync(
                HttpMethod.Post,
                "/auth/login",
                new Dictionary<string, string> { ["email"] = _options.Email, ["password"] = _options.Password },
                skipReauth: true,
                cancellationToken).ConfigureAwait(false);
        }
        catch (FellowAidenApiException)
        {
            throw new FellowAidenAuthException("Email or password incorrect.");
        }

        var login = JsonSerializer.Deserialize<LoginResponse>(text, Json);
        if (login?.AccessToken is null)
        {
            throw new FellowAidenAuthException("Email or password incorrect.");
        }

        _token = login.AccessToken;
    }

    private async Task LoadDeviceAsync(CancellationToken cancellationToken)
    {
        var devices = await SendAsync<List<DeviceConfig>>(HttpMethod.Get, "/devices?dataType=real", null, cancellationToken).ConfigureAwait(false);
        if (devices.Count == 0)
        {
            throw new FellowAidenException("No devices found on this account.");
        }

        _deviceConfig = devices[0];
        _brewerId = _deviceConfig.Id;
        _profiles = null;
        _schedules = null;
    }

    private string BrewerIdOrThrow() =>
        _brewerId ?? throw new FellowAidenException("Not authenticated. Call AuthenticateAsync or use CreateAsync.");

    private async Task<bool> IsValidProfileIdAsync(string pid, CancellationToken ct) =>
        (await GetProfilesAsync(false, ct).ConfigureAwait(false)).Any(p => p.Id == pid);

    private async Task<string> ProfileIdListAsync(CancellationToken ct) =>
        string.Join(", ", (await GetProfilesAsync(false, ct).ConfigureAwait(false)).Select(p => $"{p.Id} ({p.Title})"));

    private async Task<bool> IsValidScheduleIdAsync(string sid, CancellationToken ct) =>
        (await GetSchedulesAsync(false, ct).ConfigureAwait(false)).Any(s => s.Id == sid);

    private async Task<string> ScheduleIdListAsync(CancellationToken ct) =>
        string.Join(", ", (await GetSchedulesAsync(false, ct).ConfigureAwait(false)).Select(s => s.Id));

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var text = await SendAsync(method, path, body, skipReauth: false, cancellationToken).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<T>(text, Json);
        return result ?? throw new FellowAidenException($"Unexpected empty response from {path}.");
    }

    private async Task<string> SendAsync(HttpMethod method, string path, object? body, bool skipReauth, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, _baseUrl + path);
        request.Headers.Accept.ParseAdd("application/json");
        if (skipReauth)
        {
            request.Options.Set(RetryingHandler.SkipReauthKey, true);
        }

        if (body is not null)
        {
            request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new FellowAidenApiException((int)response.StatusCode, text);
        }

        return text;
    }
}
