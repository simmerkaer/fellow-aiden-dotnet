namespace FellowAiden;

/// <summary>Configuration for a <see cref="FellowAidenClient"/>.</summary>
public sealed record FellowAidenOptions
{
    /// <summary>Fellow account email.</summary>
    public required string Email { get; init; }

    /// <summary>Fellow account password.</summary>
    public required string Password { get; init; }

    /// <summary>API base URL. Override mainly for testing.</summary>
    public string BaseUrl { get; init; } = "https://l8qtmnc692.execute-api.us-west-2.amazonaws.com/v1";

    /// <summary>User-Agent header sent with every request.</summary>
    public string UserAgent { get; init; } = "Fellow/5 CFNetwork/1568.300.101 Darwin/24.2.0";

    /// <summary>Maximum retries for transient HTTP failures.</summary>
    public int MaxRetries { get; init; } = 3;
}
