using FellowAiden;
using Xunit;

namespace FellowAiden.Tests;

public class SimilarityTests
{
    [Fact]
    public void IdenticalStrings_ReturnOne() => Assert.Equal(1.0, Similarity.Ratio("morning blend", "morning blend"));

    [Fact]
    public void BothEmpty_ReturnsOne() => Assert.Equal(1.0, Similarity.Ratio(string.Empty, string.Empty));

    [Fact]
    public void NoOverlap_ReturnsZero() => Assert.Equal(0.0, Similarity.Ratio("abc", "xyz"));

    [Fact]
    public void CanonicalDifflibCase_AbcdBcde_Is075() =>
        Assert.Equal(0.75, Similarity.Ratio("abcd", "bcde"), 10);

    [Fact]
    public void OneCharInsertion_Is26Over27() =>
        Assert.Equal(26.0 / 27.0, Similarity.Ratio("morning blend", "morning blends"), 10);

    [Fact]
    public void FuzzyThreshold_CrossesForCloseTitleOnly()
    {
        Assert.True(Similarity.Ratio("cold brew", "cold brewer") > 0.65);
        Assert.True(Similarity.Ratio("cold brew", "espresso") < 0.65);
    }
}
