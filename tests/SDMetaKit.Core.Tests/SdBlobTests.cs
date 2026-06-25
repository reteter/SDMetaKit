namespace SDMetaKit.Core.Tests;

public class SdBlobTests
{
    private static SdMetadata SampleMeta() => new()
    {
        Prompt = "a beautiful cat",
        NegativePrompt = "ugly, blurry",
        Steps = "20",
        Sampler = "DPM++ 2M Karras",
        CfgScale = "7",
        Seed = "1234567890",
        Size = "512x512",
        Model = "v1-5-pruned",
        ModelHash = "cc6cb27103",
        Sha256 = "abcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
        OtherParams = new Dictionary<string, string> { ["ControlNet 0"] = "some_value" }
    };

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = SampleMeta();
        var blob = SdMetaKit.EncodeBlob(original);
        var decoded = SdMetaKit.DecodeBlob(blob);

        decoded.Prompt.Should().Be(original.Prompt);
        decoded.NegativePrompt.Should().Be(original.NegativePrompt);
        decoded.Steps.Should().Be(original.Steps);
        decoded.Sampler.Should().Be(original.Sampler);
        decoded.CfgScale.Should().Be(original.CfgScale);
        decoded.Seed.Should().Be(original.Seed);
        decoded.Size.Should().Be(original.Size);
        decoded.Model.Should().Be(original.Model);
        decoded.ModelHash.Should().Be(original.ModelHash);
        decoded.Sha256.Should().Be(original.Sha256);
        decoded.OtherParams.Should().ContainKey("ControlNet 0").WhoseValue.Should().Be("some_value");
    }

    [Fact]
    public void Deterministic_SameInputSameBlob()
    {
        var meta = SampleMeta();
        var blob1 = SdMetaKit.EncodeBlob(meta);
        var blob2 = SdMetaKit.EncodeBlob(meta);
        blob1.Should().Be(blob2);
    }

    [Fact]
    public void Blob_StartsWithPrefix()
    {
        var blob = SdMetaKit.EncodeBlob(SampleMeta());
        blob.Should().StartWith("SDMK1:");
    }

    [Fact]
    public void Blob_HasFile_WhenSha256Present()
    {
        var blob = SdMetaKit.EncodeBlob(SampleMeta());
        var decoded = SdMetaKit.DecodeBlob(blob);
        decoded.SourceKind.Should().Be("Blob-File");
    }

    [Fact]
    public void Blob_NoFile_WhenSha256Null()
    {
        var meta = SampleMeta() with { Sha256 = null };
        var blob = SdMetaKit.EncodeBlob(meta);
        var decoded = SdMetaKit.DecodeBlob(blob);
        decoded.SourceKind.Should().Be("Blob-Text");
        decoded.Sha256.Should().BeNull();
    }

    [Fact]
    public void TryDecode_InvalidBlob_ReturnsFalse()
    {
        var result = SdMetaKit.TryDecodeBlob("not-a-blob", out var meta);
        result.Should().BeFalse();
        meta.Should().BeNull();
    }

    [Fact]
    public void TryDecode_ValidBlob_ReturnsTrue()
    {
        var blob = SdMetaKit.EncodeBlob(SampleMeta());
        var result = SdMetaKit.TryDecodeBlob(blob, out var meta);
        result.Should().BeTrue();
        meta.Should().NotBeNull();
        meta!.Prompt.Should().Be("a beautiful cat");
    }
}
