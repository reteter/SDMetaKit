using System.Text;

namespace SDMetaKit.Core.Tests;

public class PngChunkWriterTests
{
    private readonly IPngWriter _writer = new PngChunkWriter();

    [Fact]
    public void InjectParametersText_MaliciousChunkLength_ReturnsOriginalWithoutHanging()
    {
        var png = PngTestHelper.BuildMinimalPng(withMaliciousChunk: true);

        var result = _writer.InjectParametersText(png, "Steps: 20");

        result.Should().Equal(png);
    }

    [Fact]
    public void InjectParametersText_ValidPng_InjectsParametersChunk()
    {
        var png = PngTestHelper.BuildMinimalPng();
        const string parameters = "a cat\nSteps: 20, Sampler: Euler";

        var result = _writer.InjectParametersText(png, parameters);

        result.Should().NotEqual(png);
        Encoding.UTF8.GetString(result).Should().Contain("parameters");
        Encoding.UTF8.GetString(result).Should().Contain(parameters);
    }
}

/// <summary>
/// Minimalny generator PNG do testów PngChunkWriter — bez zewnętrznych fixture.
/// </summary>
internal static class PngTestHelper
{
    private static readonly byte[] Signature =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static byte[] BuildMinimalPng(bool withMaliciousChunk = false)
    {
        using var ms = new MemoryStream();
        ms.Write(Signature);

        // IHDR: 1×1 RGBA
        var ihdr = new byte[13];
        WriteUInt32BE(ihdr, 0, 1);
        WriteUInt32BE(ihdr, 4, 1);
        ihdr[8] = 8;
        ihdr[9] = 6;
        WriteChunk(ms, "IHDR", ihdr);

        if (withMaliciousChunk)
            WriteChunkHeader(ms, "evil", 0x80000000);

        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        WriteChunkHeader(s, type, (uint)data.Length);
        s.Write(data);
        WriteUInt32BE(s, Crc32(Concat(Encoding.ASCII.GetBytes(type), data)));
    }

    private static void WriteChunkHeader(Stream s, string type, uint length)
    {
        WriteUInt32BE(s, length);
        s.Write(Encoding.ASCII.GetBytes(type));
    }

    private static void WriteUInt32BE(Stream s, uint value)
    {
        s.WriteByte((byte)(value >> 24));
        s.WriteByte((byte)(value >> 16));
        s.WriteByte((byte)(value >> 8));
        s.WriteByte((byte)value);
    }

    private static void WriteUInt32BE(byte[] buf, int offset, uint value)
    {
        buf[offset] = (byte)(value >> 24);
        buf[offset + 1] = (byte)(value >> 16);
        buf[offset + 2] = (byte)(value >> 8);
        buf[offset + 3] = (byte)value;
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }

    private static readonly uint[] Crc32Table = BuildCrc32Table();

    private static uint[] BuildCrc32Table()
    {
        const uint poly = 0xEDB88320;
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            var c = i;
            for (var k = 0; k < 8; k++)
                c = (c & 1) != 0 ? poly ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}