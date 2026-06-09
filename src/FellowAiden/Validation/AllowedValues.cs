namespace FellowAiden;

/// <summary>
/// Allowed value sets for profile parameters, mirroring the constraints
/// enforced by the Python <c>fellow-aiden</c> library and the TypeScript port.
/// </summary>
public static class AllowedValues
{
    /// <summary>Inclusive list of values from <paramref name="min"/> to <paramref name="max"/> in 0.5 steps.</summary>
    public static IReadOnlyList<double> HalfStepRange(double min, double max)
    {
        var values = new List<double>();
        for (var v = min; v <= max + 0.0001; v += 0.5)
        {
            values.Add(Math.Round(v, 1));
        }

        return values;
    }

    /// <summary>Brew-to-water ratios: 14 → 20 in 0.5 steps.</summary>
    public static readonly IReadOnlySet<double> Ratios = HalfStepRange(14, 20).ToHashSet();

    /// <summary>Bloom ratios: 1, 1.5, 2, 2.5, 3.</summary>
    public static readonly IReadOnlySet<double> BloomRatios = new HashSet<double> { 1, 1.5, 2, 2.5, 3 };

    /// <summary>Temperatures in °C: 50 → 98.5 in 0.5 steps.</summary>
    public static readonly IReadOnlySet<double> Temperatures = HalfStepRange(50, 98.5).ToHashSet();

    /// <summary>Server-managed profile fields, stripped from payloads before sending.</summary>
    internal static readonly IReadOnlySet<string> ServerSideProfileFields = new HashSet<string>(StringComparer.Ordinal)
    {
        "id", "createdAt", "deletedAt", "lastUsedTime", "sharedFrom",
        "isDefaultProfile", "instantBrew", "folder", "duration", "lastGBQuantity",
    };
}
