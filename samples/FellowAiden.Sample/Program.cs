using FellowAiden;

// Read-only smoke test against the real Fellow API.
//   setx FELLOW_EMAIL you@example.com   (or use the current shell's env)
//   dotnet run --project samples/FellowAiden.Sample

var email = Environment.GetEnvironmentVariable("FELLOW_EMAIL");
var password = Environment.GetEnvironmentVariable("FELLOW_PASSWORD");

if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
{
    Console.Error.WriteLine("Set FELLOW_EMAIL and FELLOW_PASSWORD environment variables first.");
    return 1;
}

try
{
    Console.WriteLine("Authenticating…");
    using var aiden = await FellowAidenClient.CreateAsync(new FellowAidenOptions
    {
        Email = email,
        Password = password,
    });

    Console.WriteLine($"\n☕ Brewer: {aiden.DisplayName} ({aiden.BrewerId})");

    var profiles = await aiden.GetProfilesAsync();
    Console.WriteLine($"\nProfiles ({profiles.Count}):");
    foreach (var p in profiles)
    {
        Console.WriteLine($"  • {p.Title}  [{p.Id}]");
    }

    var schedules = await aiden.GetSchedulesAsync();
    Console.WriteLine($"\nSchedules ({schedules.Count}):");
    foreach (var s in schedules)
    {
        Console.WriteLine($"  • {s.Id}");
    }

    Console.WriteLine("\n✅ Library works against the live API.");
    return 0;
}
catch (FellowAidenException ex)
{
    Console.Error.WriteLine($"\n❌ {ex.Message}");
    return 1;
}
