using System.Net;
using System.Text;

namespace FellowAiden.Tests.Fakes;

public sealed record RecordedRequest(string Method, string Path, string Query, string? Body, string? Authorization, string? UserAgent);

/// <summary>
/// Test handler that records every request and returns scripted responses via a
/// responder callback receiving the request and a zero-based call index.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, int, HttpResponseMessage> _responder;
    private int _count;

    public StubHttpMessageHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public List<RecordedRequest> Requests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        request.Headers.TryGetValues("User-Agent", out var ua);
        Requests.Add(new RecordedRequest(
            request.Method.Method,
            request.RequestUri!.AbsolutePath,
            request.RequestUri!.Query,
            body,
            request.Headers.Authorization?.ToString(),
            ua is null ? null : string.Join(' ', ua)));

        return _responder(request, _count++);
    }

    public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Empty(HttpStatusCode status) => new(status);
}
