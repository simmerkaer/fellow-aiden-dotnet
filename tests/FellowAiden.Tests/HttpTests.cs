using System.Net;
using FellowAiden;
using FellowAiden.Tests.Fakes;
using Xunit;

namespace FellowAiden.Tests;

public class HttpTests
{
    private const string BaseUrl = "https://api.test/v1";

    private static (HttpMessageInvoker Invoker, StubHttpMessageHandler Stub, Func<int> ReauthCount, Func<string?> Token)
        Build(
            Func<HttpRequestMessage, int, HttpResponseMessage> responder,
            int maxRetries = 3,
            string initialToken = "token-1")
    {
        var stub = new StubHttpMessageHandler(responder);
        var token = initialToken;
        var reauthCount = 0;
        var handler = new RetryingHandler(
            getToken: () => token,
            reauthenticate: _ =>
            {
                reauthCount++;
                token = "token-2";
                return Task.CompletedTask;
            },
            userAgent: "test-agent",
            maxRetries: maxRetries,
            delay: (_, _) => Task.CompletedTask)
        {
            InnerHandler = stub,
        };
        return (new HttpMessageInvoker(handler), stub, () => reauthCount, () => token);
    }

    private static HttpRequestMessage Get(string path = "/thing") =>
        new(HttpMethod.Get, $"{BaseUrl}{path}");

    [Fact]
    public async Task InjectsUserAgentAndBearer_AndParsesSuccess()
    {
        var (invoker, stub, _, _) = Build((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"ok\":true}"));
        var response = await invoker.SendAsync(Get(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Bearer token-1", stub.Requests[0].Authorization);
        Assert.Equal("test-agent", stub.Requests[0].UserAgent);
    }

    [Fact]
    public async Task RetriesTransientErrors_ThenSucceeds()
    {
        var (invoker, stub, _, _) = Build((_, i) => i switch
        {
            0 => StubHttpMessageHandler.Json(HttpStatusCode.InternalServerError, "{}"),
            1 => StubHttpMessageHandler.Json(HttpStatusCode.ServiceUnavailable, "{}"),
            _ => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"ok\":true}"),
        });

        var response = await invoker.SendAsync(Get(), CancellationToken.None);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, stub.Requests.Count);
    }

    [Fact]
    public async Task RetriesNetworkErrors_ThenThrowsWhenExhausted()
    {
        var (invoker, stub, _, _) = Build((_, _) => throw new HttpRequestException("boom"), maxRetries: 2);

        await Assert.ThrowsAsync<HttpRequestException>(() => invoker.SendAsync(Get(), CancellationToken.None));
        Assert.Equal(3, stub.Requests.Count); // initial + 2 retries
    }

    [Fact]
    public async Task ReauthenticatesOnce_ThenRetriesWithNewToken()
    {
        var (invoker, stub, reauthCount, _) = Build((req, _) =>
            req.Headers.Authorization?.ToString() == "Bearer token-1"
                ? StubHttpMessageHandler.Json(HttpStatusCode.Unauthorized, "{}")
                : StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"ok\":true}"));

        var response = await invoker.SendAsync(Get(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, reauthCount());
        Assert.Equal("Bearer token-1", stub.Requests[0].Authorization);
        Assert.Equal("Bearer token-2", stub.Requests[1].Authorization);
    }

    [Fact]
    public async Task SkipReauth_DoesNotRetryOn401()
    {
        var (invoker, stub, reauthCount, _) = Build((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.Unauthorized, "{}"));
        var request = Get();
        request.Options.Set(RetryingHandler.SkipReauthKey, true);

        var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, reauthCount());
        Assert.Single(stub.Requests);
    }

    [Fact]
    public async Task NonRetryableError_ReturnedAsIs()
    {
        var (invoker, stub, _, _) = Build((_, _) => StubHttpMessageHandler.Json(HttpStatusCode.BadRequest, "{\"message\":\"bad\"}"));
        var response = await invoker.SendAsync(Get(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single(stub.Requests);
    }
}
