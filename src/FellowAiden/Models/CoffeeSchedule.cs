using System.Text.RegularExpressions;

namespace FellowAiden;

/// <summary>
/// A brew schedule. Mirrors the Python <c>CoffeeSchedule</c> model.
/// Call <see cref="Validate"/> before sending.
/// </summary>
public sealed record CoffeeSchedule
{
    private static readonly Regex ProfileIdPattern = new(@"^p(local)?\d+$", RegexOptions.Compiled);

    /// <summary>Exactly 7 booleans, Sunday → Saturday.</summary>
    public required IReadOnlyList<bool> Days { get; init; }

    /// <summary>Seconds since the start of the day the brew should begin (0–86399).</summary>
    public required int SecondFromStartOfTheDay { get; init; }

    public required bool Enabled { get; init; }

    /// <summary>Water amount in millilitres (150–1500).</summary>
    public required int AmountOfWater { get; init; }

    /// <summary>Profile id: <c>p&lt;number&gt;</c> (cloud) or <c>plocal&lt;number&gt;</c> (local).</summary>
    public required string ProfileId { get; init; }

    /// <summary>Validates all fields, throwing <see cref="FellowAidenValidationException"/> if any fail.</summary>
    public void Validate()
    {
        var errors = new List<string>();

        if (Days is null || Days.Count != 7)
        {
            errors.Add("days must contain exactly 7 booleans (Sunday → Saturday)");
        }

        if (SecondFromStartOfTheDay is < 0 or > 86399)
        {
            errors.Add("secondFromStartOfTheDay must be between 0 and 86399");
        }

        if (AmountOfWater is < 150 or > 1500)
        {
            errors.Add("amountOfWater must be between 150 and 1500");
        }

        if (string.IsNullOrEmpty(ProfileId) || !ProfileIdPattern.IsMatch(ProfileId))
        {
            errors.Add("profileId must match 'p<number>' or 'plocal<number>'");
        }

        if (errors.Count > 0)
        {
            throw new FellowAidenValidationException(errors);
        }
    }
}
