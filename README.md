# FellowAiden (.NET)

[![NuGet](https://img.shields.io/nuget/v/FellowAiden.svg)](https://www.nuget.org/packages/FellowAiden)
[![NuGet downloads](https://img.shields.io/nuget/dt/FellowAiden.svg)](https://www.nuget.org/packages/FellowAiden)
[![CI](https://github.com/simmerkaer/fellow-aiden-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/simmerkaer/fellow-aiden-dotnet/actions/workflows/ci.yml)

A .NET client for the [Fellow Aiden](https://fellowproducts.com/) coffee
brewer's cloud API. A port of the TypeScript client
[`fellow-aiden`](https://www.npmjs.com/package/fellow-aiden)
([repo](https://github.com/simmerkaer/fellow-aiden-ts)), itself based on the
Python library [`9b/fellow-aiden`](https://github.com/9b/fellow-aiden).

> Unofficial — not affiliated with or endorsed by Fellow.

## Install

```sh
dotnet add package FellowAiden
```

Targets **.NET 8**. No third-party dependencies (uses `HttpClient` +
`System.Text.Json`).

## Quick start

```csharp
using FellowAiden;

// Authentication is async, so use the static factory.
using var aiden = await FellowAidenClient.CreateAsync(new FellowAidenOptions
{
    Email = Environment.GetEnvironmentVariable("FELLOW_EMAIL")!,
    Password = Environment.GetEnvironmentVariable("FELLOW_PASSWORD")!,
});

Console.WriteLine($"{aiden.DisplayName} ({aiden.BrewerId})");

foreach (var p in await aiden.GetProfilesAsync())
{
    Console.WriteLine($"{p.Title} [{p.Id}]");
}
```

## API

### Construction & auth
- `FellowAidenClient.CreateAsync(options, innerHandler?, ct)` — construct, authenticate, load the device.
- `new FellowAidenClient(options, innerHandler?)` then `await AuthenticateAsync()` — manual flow. `innerHandler` lets you inject a handler (e.g. for testing).
- `AuthenticateAsync()` — (re)log in; also called automatically once on a `401`.

`FellowAidenOptions`: `Email`, `Password`, `BaseUrl?`, `UserAgent?`, `MaxRetries?`.

### Device
- `DisplayName` / `BrewerId` properties
- `GetDeviceConfigAsync(refresh = false, ct)`
- `AdjustSettingAsync(setting, value, ct)`

### Profiles
- `GetProfilesAsync(refresh = false, ct)`
- `GetProfileByTitleAsync(title, fuzzy = false, ct)` — exact (case-insensitive) by default; fuzzy uses a difflib-equivalent ratio > 0.65.
- `CreateProfileAsync(profile, ct)` — validates first.
- `UpdateProfileAsync(profileId, profile, ct)`
- `DeleteProfileByIdAsync(profileId, ct)`
- `GenerateShareLinkAsync(profileId, ct)`
- `ParseBrewLinkUrlAsync(link, ct)` / `CreateProfileFromLinkAsync(link, ct)`

### Schedules
- `GetSchedulesAsync(refresh = false, ct)`
- `CreateScheduleAsync(schedule, ct)` — validates first.
- `ToggleScheduleAsync(scheduleId, enabled, ct)`
- `DeleteScheduleByIdAsync(scheduleId, ct)`

### Validation

`CoffeeProfile.Validate()` and `CoffeeSchedule.Validate()` enforce the same
rules as the Python/TS originals (ratio 14–20 step 0.5, temperatures 50–98.5
step 0.5, bloom ratios, ranges, title charset, schedule days/water/profileId)
and throw `FellowAidenValidationException` with all failures. The allowed grids
are exposed on `AllowedValues`. The client validates automatically on
create/update. Other failures throw `FellowAidenApiException` (status + body)
or `FellowAidenAuthException`; all derive from `FellowAidenException`.

## Sample

[`samples/FellowAiden.Sample`](./samples/FellowAiden.Sample) — set
`FELLOW_EMAIL` / `FELLOW_PASSWORD` then `dotnet run --project samples/FellowAiden.Sample`.

## Releasing

Bump `<Version>` in `src/FellowAiden/FellowAiden.csproj`, then tag and push:

```sh
git tag v1.2.3
git push --follow-tags
```

The tag triggers [`release.yml`](./.github/workflows/release.yml), which builds,
tests, packs, and publishes to NuGet via
[trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
(OIDC — no API key stored).

## License

MIT.
