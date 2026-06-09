using System.Text.RegularExpressions;

namespace FellowAiden;

/// <summary>
/// A coffee brewing profile. Mirrors the Python <c>CoffeeProfile</c> model;
/// all fields are required. Call <see cref="Validate"/> before sending.
/// </summary>
public sealed record CoffeeProfile
{
    private static readonly Regex TitlePattern =
        new(@"^[A-Za-z0-9 !@#$%&*\-+?/.,:)(]+$", RegexOptions.Compiled);

    public required int ProfileType { get; init; }

    public required string Title { get; init; }

    public required double Ratio { get; init; }

    public required bool BloomEnabled { get; init; }

    public required double BloomRatio { get; init; }

    public required int BloomDuration { get; init; }

    public required double BloomTemperature { get; init; }

    public required bool SsPulsesEnabled { get; init; }

    public required int SsPulsesNumber { get; init; }

    public required int SsPulsesInterval { get; init; }

    public required IReadOnlyList<double> SsPulseTemperatures { get; init; }

    public required bool BatchPulsesEnabled { get; init; }

    public required int BatchPulsesNumber { get; init; }

    public required int BatchPulsesInterval { get; init; }

    public required IReadOnlyList<double> BatchPulseTemperatures { get; init; }

    /// <summary>Validates all fields, throwing <see cref="FellowAidenValidationException"/> if any fail.</summary>
    public void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(Title) || Title.Length > 50 || !TitlePattern.IsMatch(Title))
        {
            errors.Add("title must be 1–50 characters using the allowed character set");
        }

        if (!AllowedValues.Ratios.Contains(Ratio))
        {
            errors.Add("ratio must be between 14 and 20 in 0.5 steps");
        }

        if (!AllowedValues.BloomRatios.Contains(BloomRatio))
        {
            errors.Add("bloomRatio must be one of 1, 1.5, 2, 2.5, 3");
        }

        if (BloomDuration is < 1 or > 120)
        {
            errors.Add("bloomDuration must be between 1 and 120");
        }

        if (!AllowedValues.Temperatures.Contains(BloomTemperature))
        {
            errors.Add("bloomTemperature must be between 50 and 98.5 in 0.5 steps");
        }

        if (SsPulsesNumber is < 1 or > 10)
        {
            errors.Add("ssPulsesNumber must be between 1 and 10");
        }

        if (SsPulsesInterval is < 5 or > 60)
        {
            errors.Add("ssPulsesInterval must be between 5 and 60");
        }

        ValidateTemperatures(SsPulseTemperatures, "ssPulseTemperatures", errors);

        if (BatchPulsesNumber is < 1 or > 10)
        {
            errors.Add("batchPulsesNumber must be between 1 and 10");
        }

        if (BatchPulsesInterval is < 5 or > 60)
        {
            errors.Add("batchPulsesInterval must be between 5 and 60");
        }

        ValidateTemperatures(BatchPulseTemperatures, "batchPulseTemperatures", errors);

        if (errors.Count > 0)
        {
            throw new FellowAidenValidationException(errors);
        }
    }

    private static void ValidateTemperatures(
        IReadOnlyList<double> temperatures, string field, List<string> errors)
    {
        if (temperatures is null || temperatures.Any(t => !AllowedValues.Temperatures.Contains(t)))
        {
            errors.Add($"{field} values must each be between 50 and 98.5 in 0.5 steps");
        }
    }
}
