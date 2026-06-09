using FellowAiden;
using Xunit;

namespace FellowAiden.Tests;

public class ValidationTests
{
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
        SecondFromStartOfTheDay = 7 * 3600,
        Enabled = true,
        AmountOfWater = 300,
        ProfileId = "p3",
    };

    [Fact]
    public void ValidProfile_DoesNotThrow() => ValidProfile().Validate();

    [Fact]
    public void Ratio_OutOfRange_Throws() =>
        Assert.Throws<FellowAidenValidationException>(() => (ValidProfile() with { Ratio = 13 }).Validate());

    [Fact]
    public void Temperature_OffHalfStepGrid_Throws() =>
        Assert.Throws<FellowAidenValidationException>(() => (ValidProfile() with { BloomTemperature = 96.2 }).Validate());

    [Fact]
    public void Title_TooLong_Throws() =>
        Assert.Throws<FellowAidenValidationException>(() => (ValidProfile() with { Title = new string('x', 51) }).Validate());

    [Fact]
    public void Title_UnsupportedChars_Throws() =>
        Assert.Throws<FellowAidenValidationException>(() => (ValidProfile() with { Title = "Bad\ttitle" }).Validate());

    [Fact]
    public void PulseTemperature_OffGrid_Throws() =>
        Assert.Throws<FellowAidenValidationException>(() =>
            (ValidProfile() with { SsPulseTemperatures = new double[] { 96, 200 } }).Validate());

    [Fact]
    public void ValidationException_AggregatesAllErrors()
    {
        var ex = Assert.Throws<FellowAidenValidationException>(() =>
            (ValidProfile() with { Ratio = 13, BloomDuration = 999 }).Validate());
        Assert.Equal(2, ex.Errors.Count);
    }

    [Fact]
    public void ValidSchedule_DoesNotThrow() => ValidSchedule().Validate();

    [Fact]
    public void PlocalProfileId_IsAccepted() => (ValidSchedule() with { ProfileId = "plocal12" }).Validate();

    [Fact]
    public void Days_WrongLength_Throws() =>
        Assert.Throws<FellowAidenValidationException>(() => (ValidSchedule() with { Days = new[] { true, false } }).Validate());

    [Theory]
    [InlineData(100)]
    [InlineData(2000)]
    public void Water_OutOfRange_Throws(int water) =>
        Assert.Throws<FellowAidenValidationException>(() => (ValidSchedule() with { AmountOfWater = water }).Validate());

    [Fact]
    public void BadProfileId_Throws() =>
        Assert.Throws<FellowAidenValidationException>(() => (ValidSchedule() with { ProfileId = "x9" }).Validate());

    [Fact]
    public void SecondOfDay_OutOfRange_Throws() =>
        Assert.Throws<FellowAidenValidationException>(() => (ValidSchedule() with { SecondFromStartOfTheDay = 86400 }).Validate());
}
