using System.Text.Json;
using System.Text.Json.Serialization;

namespace FellowAiden;

/// <summary>A brewer device's configuration. Unknown fields are preserved in <see cref="Extra"/>.</summary>
public sealed record DeviceConfig
{
    public string? Id { get; init; }

    public string? DisplayName { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// <summary>A profile as returned by the API. Unknown fields are preserved in <see cref="Extra"/>.</summary>
public sealed record Profile
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

/// <summary>A schedule as returned by the API. Unknown fields are preserved in <see cref="Extra"/>.</summary>
public sealed record Schedule
{
    public string Id { get; init; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; init; }
}

internal sealed record LoginResponse
{
    public string? AccessToken { get; init; }

    public string? RefreshToken { get; init; }
}
