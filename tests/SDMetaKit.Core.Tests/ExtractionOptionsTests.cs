namespace SDMetaKit.Core.Tests;

public class ExtractionOptionsTests
{
    [Fact]
    public void Default_HasExpectedValues()
    {
        ExtractionOptions.Default.MinimumTextLength.Should().Be(150);
        ExtractionOptions.Default.PreferStepsCandidate.Should().BeTrue();
        ExtractionOptions.Default.ComputeSha256.Should().BeTrue();
        ExtractionOptions.Default.CollectAllTags.Should().BeTrue();
    }

    [Fact]
    public void NoLengthFilter_HasZeroMinimumLength()
    {
        ExtractionOptions.NoLengthFilter.MinimumTextLength.Should().Be(0);
    }
}

/// <summary>
/// Testy ParseKeyValue (wewnętrzna metoda, testowana przez A1111Parser.Parse).
/// Weryfikują graniczne przypadki parsera klucz=wartość.
/// </summary>
public class ParseKeyValueTests
{
    [Fact]
    public void ParseKeyValue_HandlesMultipleSpaces()
    {
        var result = A1111Parser.ParseKeyValue("Steps: 20,  Sampler: Euler a");
        result.Should().ContainKey("Steps").WhoseValue.Should().Be("20");
        result.Should().ContainKey("Sampler").WhoseValue.Should().Be("Euler a");
    }

    [Fact]
    public void ParseKeyValue_HandlesNewlineSeparatedPairs()
    {
        var result = A1111Parser.ParseKeyValue("Steps: 20\nSeed: 999");
        result["Steps"].Should().Be("20");
        result["Seed"].Should().Be("999");
    }

    [Fact]
    public void ParseKeyValue_EmptyInput_ReturnsEmptyDict()
    {
        A1111Parser.ParseKeyValue("").Should().BeEmpty();
        A1111Parser.ParseKeyValue("   ").Should().BeEmpty();
    }
}
