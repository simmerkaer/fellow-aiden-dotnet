using System.Net;
using System.Net.Http.Headers;

namespace FellowAiden;

/// <summary>
/// Delegating handler that injects the Fellow <c>User-Agent</c> and bearer
/// token, retries transient failures, and transparently re-authenticates once
/// on a 401. The equivalent of the TypeScript client's HTTP wrapper.
/// </summary>
internal sealed class RetryingHandler : DelegatingHandler
{
    /// <summary>Per-request flag: when set, a 401 is not retried (used by the login request itself).</summary>
    internal static readonly HttpRequestOptionsKey<bool> SkipReauthKey = new("FellowAiden.SkipReauth");

    private static readonly HashSet<HttpStatusCode> RetryableStatuses = new()
    {
        HttpStatusCode.RequestTimeout, // 408
        HttpStatusCode.InternalServerError, // 500
        HttpStatusCode.BadGateway, // 502
        HttpStatusCode.ServiceUnavailable, // 503
        HttpStatusCode.GatewayTimeout, // 504
    };

    private readonly Func<string?> _getToken;
    private readonly Func<CancellationToken, Task> _reauthenticate;
    private readonly string _userAgent;
    private readonly int _maxRetries;
    private readonly Func<int, CancellationToken, Task> _delay;

    public RetryingHandler(
        Func<string?> getToken,
        Func<CancellationToken, Task> reauthenticate,
        string userAgent,
        int maxRetries,
        Func<int, CancellationToken, Task>? delay = null)
    {
        _getToken = getToken;
        _reauthenticate = reauthenticate;
        _userAgent = userAgent;
        _maxRetries = maxRetries;
        _delay = delay ?? ((attempt, ct) => Task.Delay(300 * attempt, ct));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var skipReauth = request.Options.TryGetValue(SkipReauthKey, out var skip) && skip;

        // Buffer the body so the request can be re-sent on retry.
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentType = request.Content?.Headers.ContentType;

        var reauthed = false;
        for (var attempt = 0; ; attempt++)
        {
            using var clone = CloneRequest(request, body, contentType);

            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(clone, cancellationToken);
            }
            catch (HttpRequestException) when (attempt < _maxRetries)
            {
                await _delay(attempt + 1, cancellationToken);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized && !reauthed && !skipReauth)
            {
                reauthed = true;
                response.Dispose();
                await _reauthenticate(cancellationToken);
                continue; // retry once with the refreshed token (not counted as a retry attempt)
            }

            if (RetryableStatuses.Contains(response.StatusCode) && attempt < _maxRetries)
            {
                response.Dispose();
                await _delay(attempt + 1, cancellationToken);
                continue;
            }

            return response;
        }
    }

    private HttpRequestMessage CloneRequest(
        HttpRequestMessage original, byte[]? body, MediaTypeHeaderValue? contentType)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
        };

        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            if (contentType is not null)
            {
                clone.Content.Headers.ContentType = contentType;
            }
        }

        // User-Agent set without validation (the Fellow string isn't a strict product token).
        clone.Headers.Remove("User-Agent");
        clone.Headers.TryAddWithoutValidation("User-Agent", _userAgent);

        var token = _getToken();
        if (!string.IsNullOrEmpty(token))
        {
            clone.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return clone;
    }
}
