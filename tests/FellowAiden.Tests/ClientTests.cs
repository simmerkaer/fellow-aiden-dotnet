using System.Net;
using FellowAiden;
using FellowAiden.Tests.Fakes;
using Xunit;

namespace FellowAiden.Tests;

public class ClientTests
{
    private const string LoginOk = "{\"accessToken\":\"tok\",\"refreshToken\":\"ref\"}";
    private const string DeviceList = "[{\"id\":\"dev-1\",\"displayName\":\"Kitchen Aiden\"}]";

    private const string SharedProfileJson = """
    {
      "id":"p5","createdAt":"x","folder":"f",
      "profileType":0,"title":"Morning Blend","ratio":16,
      "bloomEnabled":true,"bloomRatio":2,"bloomDuration":30,"bloomTemperature":96,
      "ssPulsesEnabled":true,"ssPulsesNumber":3,"ssPulsesInterval":20,"ssPulseTemperatures":[96,96.5],
      "batchPulsesEnabled":false,"batchPulsesNumber":1,"batchPulsesInterval":10,"batchPulseTemperatures":[95]
    }
    """;

    private static CoffeeProfile ValidProfile() => new()
    {
        ProfileType = 0,
        Title = "Morning Blend",
        Ratio = 16,
        BloomEnabled = true,
        BloomRatio = 2,
        BloomDuration = 30,
        BloomTemperature = 96,
        SsPulsesEnabled = true,
        SsPulsesNumber = 3,
        SsPulsesInterval = 20,
        SsPulseTemperatures = new double[] { 96, 96.5 },
        BatchPulsesEnabled = false,
        BatchPulsesNumber = 1,
        BatchPulsesInterval = 10,
        BatchPulseTemperatures = new double[] { 95 },
    };

    private static CoffeeSchedule ValidSchedule() => new()
    {
        Days = new[] { false, true, true, true, true, true, false },
        SecondFromStartOfTheDay = 25200,
        Enabled = true,
        AmountOfWater = 300,
        ProfileId = "p3",
    };

    private static StubHttpMessageHandler MakeStub(
        Dictionary<string, Func<HttpResponseMessage>> routes,
        string loginResponse = LoginOk,
        HttpStatusCode loginStatus = HttpStatusCode.OK,
        string deviceList = DeviceList)
    {
        var table = new Dictionary<string, Func<HttpResponseMessage>>
        {
            ["POST /auth/login"] = () => StubHttpMessageHandler.Json(loginStatus, loginResponse),
            ["GET /devices"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, deviceList),
        };
        foreach (var route in routes)
        {
            table[route.Key] = route.Value;
        }

        return new StubHttpMessageHandler((req, _) =>
        {
            var key = $"{req.Method.Method} {req.RequestUri!.AbsolutePath}";
            return table.TryGetValue(key, out var responder)
                ? responder()
                : StubHttpMessageHandler.Json(HttpStatusCode.NotFound, "{\"error\":\"not found\"}");
        });
    }

    private static Task<FellowAidenClient> MakeClientAsync(
        Dictionary<string, Func<HttpResponseMessage>>? routes = null) =>
        FellowAidenClient.CreateAsync(
            new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" },
            MakeStub(routes ?? new()));

    [Fact]
    public async Task CreateAsync_AuthenticatesAndLoadsDevice()
    {
        using var client = await MakeClientAsync();
        Assert.Equal("dev-1", client.BrewerId);
        Assert.Equal("Kitchen Aiden", client.DisplayName);
    }

    [Fact]
    public async Task BadCredentials_ThrowsAuthException()
    {
        var stub = MakeStub(new(), loginStatus: HttpStatusCode.Unauthorized, loginResponse: "{\"error\":\"bad\"}");
        await Assert.ThrowsAsync<FellowAidenAuthException>(() =>
            FellowAidenClient.CreateAsync(new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" }, stub));
    }

    [Fact]
    public async Task NoDevices_Throws()
    {
        var stub = MakeStub(new(), deviceList: "[]");
        await Assert.ThrowsAsync<FellowAidenException>(() =>
            FellowAidenClient.CreateAsync(new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" }, stub));
    }

    [Fact]
    public async Task GetProfiles_IsCached()
    {
        var stub = MakeStub(new()
        {
            ["GET /devices/dev-1/profiles"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, "[{\"id\":\"p1\",\"title\":\"House\"}]"),
        });
        using var client = await FellowAidenClient.CreateAsync(new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" }, stub);

        await client.GetProfilesAsync();
        await client.GetProfilesAsync();

        Assert.Equal(1, stub.Requests.Count(r => r is { Method: "GET", Path: "/devices/dev-1/profiles" }));
    }

    [Fact]
    public async Task GetProfileByTitle_ExactAndFuzzy()
    {
        const string profiles = "[{\"id\":\"p1\",\"title\":\"Morning Blend\"},{\"id\":\"p2\",\"title\":\"Cold Brewer\"}]";
        using var client = await MakeClientAsync(new()
        {
            ["GET /devices/dev-1/profiles"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, profiles),
        });

        Assert.Equal("p1", (await client.GetProfileByTitleAsync("morning blend"))!.Id);
        Assert.Null(await client.GetProfileByTitleAsync("nope"));
        Assert.Equal("p2", (await client.GetProfileByTitleAsync("cold brew", fuzzy: true))!.Id);
        Assert.Null(await client.GetProfileByTitleAsync("cold brew", fuzzy: false));
    }

    [Fact]
    public async Task CreateProfile_PostsValidatedBody()
    {
        var stub = MakeStub(new()
        {
            ["POST /devices/dev-1/profiles"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"id\":\"p9\",\"title\":\"Morning Blend\"}"),
        });
        using var client = await FellowAidenClient.CreateAsync(new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" }, stub);

        var created = await client.CreateProfileAsync(ValidProfile());

        Assert.Equal("p9", created.Id);
        var post = stub.Requests.Single(r => r is { Method: "POST", Path: "/devices/dev-1/profiles" });
        Assert.Contains("\"title\":\"Morning Blend\"", post.Body);
        Assert.Contains("\"profileType\":0", post.Body);
        Assert.DoesNotContain("\"createdAt\"", post.Body);
    }

    [Fact]
    public async Task CreateProfile_InvalidThrowsValidation()
    {
        using var client = await MakeClientAsync();
        await Assert.ThrowsAsync<FellowAidenValidationException>(() =>
            client.CreateProfileAsync(ValidProfile() with { Ratio = 99 }));
    }

    [Fact]
    public async Task DeleteProfileById_SendsDelete()
    {
        var stub = MakeStub(new()
        {
            ["DELETE /devices/dev-1/profiles/p1"] = () => StubHttpMessageHandler.Empty(HttpStatusCode.OK),
        });
        using var client = await FellowAidenClient.CreateAsync(new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" }, stub);

        await client.DeleteProfileByIdAsync("p1");
        Assert.Contains(stub.Requests, r => r is { Method: "DELETE", Path: "/devices/dev-1/profiles/p1" });
    }

    [Fact]
    public async Task GenerateShareLink_ReturnsLink()
    {
        using var client = await MakeClientAsync(new()
        {
            ["POST /devices/dev-1/profiles/p1/share"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"link\":\"https://brew.link/p/abc123\"}"),
        });
        Assert.Equal("https://brew.link/p/abc123", await client.GenerateShareLinkAsync("p1"));
    }

    [Fact]
    public async Task ParseBrewLinkUrl_StripsServerFields()
    {
        using var client = await MakeClientAsync(new()
        {
            ["GET /shared/abc123"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, SharedProfileJson),
        });

        var profile = await client.ParseBrewLinkUrlAsync("https://brew.link/p/abc123");
        Assert.Equal("Morning Blend", profile.Title);
        Assert.Equal(16, profile.Ratio);
    }

    [Fact]
    public async Task CreateProfileFromLink_EndToEnd()
    {
        var stub = MakeStub(new()
        {
            ["GET /shared/abc123"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, SharedProfileJson),
            ["POST /devices/dev-1/profiles"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"id\":\"p10\",\"title\":\"Morning Blend\"}"),
        });
        using var client = await FellowAidenClient.CreateAsync(new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" }, stub);

        var created = await client.CreateProfileFromLinkAsync("https://brew.link/p/abc123");

        Assert.Equal("p10", created.Id);
        var post = stub.Requests.Single(r => r is { Method: "POST", Path: "/devices/dev-1/profiles" });
        Assert.DoesNotContain("\"id\"", post.Body);
    }

    [Fact]
    public async Task CreateSchedule_Posts()
    {
        using var client = await MakeClientAsync(new()
        {
            ["POST /devices/dev-1/schedules"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, "{\"id\":\"s1\"}"),
        });
        var created = await client.CreateScheduleAsync(ValidSchedule());
        Assert.Equal("s1", created.Id);
    }

    [Fact]
    public async Task CreateSchedule_InvalidThrows()
    {
        using var client = await MakeClientAsync();
        await Assert.ThrowsAsync<FellowAidenValidationException>(() =>
            client.CreateScheduleAsync(ValidSchedule() with { AmountOfWater = 5 }));
    }

    [Fact]
    public async Task ToggleSchedule_ValidatesAndPatches()
    {
        var stub = MakeStub(new()
        {
            ["GET /devices/dev-1/schedules"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, "[{\"id\":\"s1\"}]"),
            ["PATCH /devices/dev-1/schedules/s1"] = () => StubHttpMessageHandler.Empty(HttpStatusCode.OK),
        });
        using var client = await FellowAidenClient.CreateAsync(new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" }, stub);

        await client.ToggleScheduleAsync("s1", false);
        var patch = stub.Requests.Single(r => r is { Method: "PATCH", Path: "/devices/dev-1/schedules/s1" });
        Assert.Equal("{\"enabled\":false}", patch.Body);

        await Assert.ThrowsAsync<FellowAidenException>(() => client.ToggleScheduleAsync("nope", true));
    }

    [Fact]
    public async Task DeleteSchedule_ValidatesAndDeletes()
    {
        var stub = MakeStub(new()
        {
            ["GET /devices/dev-1/schedules"] = () => StubHttpMessageHandler.Json(HttpStatusCode.OK, "[{\"id\":\"s1\"}]"),
            ["DELETE /devices/dev-1/schedules/s1"] = () => StubHttpMessageHandler.Empty(HttpStatusCode.OK),
        });
        using var client = await FellowAidenClient.CreateAsync(new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" }, stub);

        await client.DeleteScheduleByIdAsync("s1");
        Assert.Contains(stub.Requests, r => r is { Method: "DELETE", Path: "/devices/dev-1/schedules/s1" });
    }

    [Fact]
    public async Task AdjustSetting_PatchesDevice()
    {
        var stub = MakeStub(new()
        {
            ["PATCH /devices/dev-1"] = () => StubHttpMessageHandler.Empty(HttpStatusCode.OK),
        });
        using var client = await FellowAidenClient.CreateAsync(new FellowAidenOptions { Email = "a@b.c", Password = "pw", BaseUrl = "https://api.test" }, stub);

        await client.AdjustSettingAsync("displayName", "New Name");
        var patch = stub.Requests.Single(r => r is { Method: "PATCH", Path: "/devices/dev-1" });
        Assert.Equal("{\"displayName\":\"New Name\"}", patch.Body);
    }

    [Fact]
    public void Constructor_RequiresEmailAndPassword() =>
        Assert.Throws<FellowAidenException>(() => new FellowAidenClient(new FellowAidenOptions { Email = "", Password = "" }));
}
