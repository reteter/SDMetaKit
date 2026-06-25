namespace SDMetaKit.Core.Tests;

public class A1111ParserTests
{
    private readonly IMetadataParser _parser = new A1111Parser();

    private const string FullSample =
        "a beautiful landscape, mountains, 4k, highly detailed\n" +
        "Negative prompt: ugly, blurry, watermark\n" +
        "Steps: 20, Sampler: DPM++ 2M Karras, Schedule type: Karras, CFG scale: 7, " +
        "Seed: 1234567890, Size: 512x768, Model: v1-5-pruned, Model hash: cc6cb27103, " +
        "Denoising strength: 0.7";

    [Fact]
    public void Parse_FullSample_ExtractsAllPriorityFields()
    {
        var result = _parser.Parse(FullSample);

        result.Prompt.Should().Be("a beautiful landscape, mountains, 4k, highly detailed");
        result.NegativePrompt.Should().Be("ugly, blurry, watermark");
        result.Steps.Should().Be("20");
        result.Sampler.Should().Be("DPM++ 2M Karras");
        result.ScheduleType.Should().Be("Karras");
        result.CfgScale.Should().Be("7");
        result.Seed.Should().Be("1234567890");
        result.Size.Should().Be("512x768");
        result.Model.Should().Be("v1-5-pruned");
        result.ModelHash.Should().Be("cc6cb27103");
        result.DenoisingStrength.Should().Be("0.7");
    }

    [Fact]
    public void Parse_WithoutNegativePrompt_SetsEmptyNegative()
    {
        const string input = "photo of a cat\nSteps: 10, Sampler: Euler";
        var result = _parser.Parse(input);

        result.Prompt.Should().Be("photo of a cat");
        result.NegativePrompt.Should().BeEmpty();
        result.Steps.Should().Be("10");
        result.Sampler.Should().Be("Euler");
    }

    [Fact]
    public void Parse_WithoutStepsBlock_ReturnsRawPrompt()
    {
        const string input = "just a simple prompt without any parameters";
        var result = _parser.Parse(input);

        result.Prompt.Should().Be(input);
        result.NegativePrompt.Should().BeEmpty();
        result.Steps.Should().BeNull();
        result.Sampler.Should().BeNull();
    }

    [Fact]
    public void Parse_EmptyOrWhiteSpace_ReturnsEmpty()
    {
        _parser.Parse("").Prompt.Should().BeEmpty();
        _parser.Parse("   ").Prompt.Should().BeEmpty();
    }

    [Fact]
    public void Parse_QuotedValue_HandlesEscapedQuotes()
    {
        const string input = "prompt\nSteps: 1, Model: \"my \\\"special\\\" model\"";
        var result = _parser.Parse(input);

        result.Model.Should().Be("my \"special\" model");
    }

    [Fact]
    public void Parse_UnknownKeys_GoToOtherParams()
    {
        const string input = "prompt\nSteps: 20, ControlNet 0: some_value, Lora hashes: abc123";
        var result = _parser.Parse(input);

        result.Steps.Should().Be("20");
        result.OtherParams.Should().ContainKey("ControlNet 0").WhoseValue.Should().Be("some_value");
        result.OtherParams.Should().ContainKey("Lora hashes");
    }

    [Fact]
    public void Parse_HiresParams_ExtractedCorrectly()
    {
        const string input = "prompt\nSteps: 20, Hires upscale: 2, Hires steps: 10, Hires upscaler: 4x-UltraSharp";
        var result = _parser.Parse(input);

        result.HiresUpscale.Should().Be("2");
        result.HiresSteps.Should().Be("10");
        result.HiresUpscaler.Should().Be("4x-UltraSharp");
    }

    [Fact]
    public void Parse_NegativePromptWithoutSteps_CapturesNegative()
    {
        const string input = "positive prompt\nNegative prompt: bad stuff";
        var result = _parser.Parse(input);

        result.Prompt.Should().Be("positive prompt");
        result.NegativePrompt.Should().Be("bad stuff");
        result.Steps.Should().BeNull();
    }

    [Fact]
    public void Parse_RawTextIsPreserved()
    {
        var result = _parser.Parse(FullSample);
        result.RawText.Should().Be(FullSample);
    }

    [Fact]
    public void Parse_SourceKindIsText()
    {
        var result = _parser.Parse(FullSample);
        result.SourceKind.Should().Be("Text");
    }
}
