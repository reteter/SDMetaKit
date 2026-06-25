using System.Security.Cryptography;

namespace SDMetaKit.Core.Tests;

public class FileMetadataExtractorTests
{
    private const string SampleParametersText =
        "a beautiful landscape, mountains, 4k, highly detailed\n" +
        "Negative prompt: ugly, blurry, watermark\n" +
        "Steps: 20, Sampler: DPM++ 2M Karras, CFG scale: 7, " +
        "Seed: 1234567890, Size: 512x768, Model: v1-5-pruned, Model hash: cc6cb27103";

    [Fact]
    public async Task ExtractAsync_Stream_PngWithParameters_ReturnsParametersTextAndSha256()
    {
        var rawPng = PngTestHelper.BuildMinimalPng();
        var png = SdMetaKit.InjectParametersText(rawPng, SampleParametersText);
        using var stream = new MemoryStream(png);

        var extractor = new FileMetadataExtractor();
        var raw = await extractor.ExtractAsync(stream, ExtractionOptions.NoLengthFilter);

        raw.ParametersText.Should().Be(SampleParametersText);
        raw.SourceKind.Should().Be("PNG-tEXt");
        raw.Sha256.Should().NotBeNull();
        raw.Sha256.Should().Be(Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant());
    }

    [Fact]
    public async Task ExtractAsync_Stream_PngWithParameters_CollectsAllTags()
    {
        var rawPng = PngTestHelper.BuildMinimalPng();
        var png = SdMetaKit.InjectParametersText(rawPng, SampleParametersText);
        using var stream = new MemoryStream(png);

        var extractor = new FileMetadataExtractor();
        var raw = await extractor.ExtractAsync(stream, ExtractionOptions.NoLengthFilter);

        raw.AllTags.Should().ContainKey("PNG-tEXt/Textual Data");
        raw.AllTags["PNG-tEXt/Textual Data"].Should().StartWith("parameters: ");
        raw.AllTags["PNG-tEXt/Textual Data"].Should().Contain(SampleParametersText);
    }

    [Fact]
    public async Task ExtractAsync_EndToEnd_PngText_ParsesMetadata()
    {
        var rawPng = PngTestHelper.BuildMinimalPng();
        var png = SdMetaKit.InjectParametersText(rawPng, SampleParametersText);
        using var stream = new MemoryStream(png);

        var extractor = new FileMetadataExtractor();
        var raw = await extractor.ExtractAsync(stream, ExtractionOptions.NoLengthFilter);
        var meta = SdMetaKit.Parse(raw.ParametersText!);

        meta.Prompt.Should().Be("a beautiful landscape, mountains, 4k, highly detailed");
        meta.NegativePrompt.Should().Be("ugly, blurry, watermark");
        meta.Steps.Should().Be("20");
        meta.Sampler.Should().Be("DPM++ 2M Karras");
        meta.CfgScale.Should().Be("7");
        meta.Seed.Should().Be("1234567890");
        meta.Size.Should().Be("512x768");
        meta.Model.Should().Be("v1-5-pruned");
        meta.ModelHash.Should().Be("cc6cb27103");
    }
}
