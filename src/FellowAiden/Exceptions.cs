namespace FellowAiden;

/// <summary>Base class for all errors thrown by the FellowAiden client.</summary>
public class FellowAidenException : Exception
{
    public FellowAidenException(string message)
        : base(message)
    {
    }

    public FellowAidenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Authentication failed (bad credentials, or re-auth on a 401 did not recover).</summary>
public sealed class FellowAidenAuthException : FellowAidenException
{
    public FellowAidenAuthException(string message)
        : base(message)
    {
    }
}

/// <summary>The API returned a non-success status code.</summary>
public sealed class FellowAidenApiException : FellowAidenException
{
    public FellowAidenApiException(int statusCode, string? body)
        : base($"Fellow Aiden API responded with status {statusCode}")
    {
        StatusCode = statusCode;
        Body = body;
    }

    public int StatusCode { get; }

    public string? Body { get; }
}

/// <summary>Input failed validation before being sent to the API.</summary>
public sealed class FellowAidenValidationException : FellowAidenException
{
    public FellowAidenValidationException(IReadOnlyList<string> errors)
        : base("Validation failed: " + string.Join("; ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
