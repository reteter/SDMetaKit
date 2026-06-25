namespace SDMetaKit.Api.Tests;

public class ApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private const string SampleParams =
        "a cat\nNegative prompt: ugly\nSteps: 20, Sampler: Euler, CFG scale: 7, Seed: 42, Size: 512x512, Model: test";

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var resp = await _client.GetAsync("/health");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadAsStringAsync();
        body.Should().Contain("ok");
    }

    [Fact]
    public async Task ParseText_ValidInput_ReturnsParsedMetadata()
    {
        var resp = await _client.PostAsJsonAsync("/v1/parse/text", new { text = SampleParams });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var meta = await resp.Content.ReadFromJsonAsync<SdMetadata>();
        meta.Should().NotBeNull();
        meta!.Prompt.Should().Be("a cat");
        meta.Steps.Should().Be("20");
        meta.Sampler.Should().Be("Euler");
    }

    [Fact]
    public async Task ParseText_EmptyText_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/v1/parse/text", new { text = "" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task BlobFromText_AndDecode_RoundTrip()
    {
        var fromTextResp = await _client.PostAsJsonAsync("/v1/blob/from-text", new { text = SampleParams });
        fromTextResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var fromTextBody = await fromTextResp.Content.ReadFromJsonAsync<BlobResponse>();
        fromTextBody!.Blob.Should().StartWith("SDMK1:");

        var decodeResp = await _client.PostAsJsonAsync("/v1/blob/decode", new { blob = fromTextBody.Blob });
        decodeResp.StatusCode.Should().Be(HttpStatusCode.OK);

        var meta = await decodeResp.Content.ReadFromJsonAsync<SdMetadata>();
        meta!.Prompt.Should().Be("a cat");
        meta.Steps.Should().Be("20");
    }

    [Fact]
    public async Task BlobDecode_InvalidBlob_ReturnsBadRequest()
    {
        var resp = await _client.PostAsJsonAsync("/v1/blob/decode", new { blob = "not-a-blob" });
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private record BlobResponse(string Blob);
}
