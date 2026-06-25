using System.Text;

namespace SDMetaKit;

/// <summary>
/// Wstrzykuje chunk PNG-tEXt z kluczem "parameters" przed IEND.
/// Port PngMetadataWriter z SDForgePanel.
/// </summary>
public sealed class PngChunkWriter : IPngWriter
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>
    /// Zwraca nowy bufor PNG z wstrzykniętym chunkiem tEXt.
    /// Gdy wejście nie jest poprawnym PNG, zwraca oryginalne bajty bez zmian.
    /// </summary>
    public byte[] InjectParametersText(byte[] png, string parametersText)
    {
        if (png.Length < PngSignature.Length || !HasPngSignature(png))
            return png;

        try
        {
            var keyword = Encoding.ASCII.GetBytes("parameters");
            var textBytes = Encoding.UTF8.GetBytes(parametersText);

            var chunkData = new byte[keyword.Length + 1 + textBytes.Length];
            Buffer.BlockCopy(keyword, 0, chunkData, 0, keyword.Length);
            chunkData[keyword.Length] = 0;
            Buffer.BlockCopy(textBytes, 0, chunkData, keyword.Length + 1, textBytes.Length);

            var chunkType = Encoding.ASCII.GetBytes("tEXt");
            var crc = ComputeCrc32(Concat(chunkType, chunkData));

            var iendOffset = FindIendOffset(png, PngSignature.Length);
            if (iendOffset < 0) return png;

            using var ms = new MemoryStream();
            ms.Write(png, 0, iendOffset);

            WriteUInt32BE(ms, (uint)chunkData.Length);
            ms.Write(chunkType, 0, chunkType.Length);
            ms.Write(chunkData, 0, chunkData.Length);
            WriteUInt32BE(ms, crc);

            ms.Write(png, iendOffset, png.Length - iendOffset);
            return ms.ToArray();
        }
        catch
        {
            return png;
        }
    }

    private static bool HasPngSignature(byte[] data)
    {
        for (var i = 0; i < PngSignature.Length; i++)
            if (data[i] != PngSignature[i]) return false;
        return true;
    }

    private static int FindIendOffset(byte[] png, int start)
    {
        var pos = start;
        while (pos + 8 <= png.Length)
        {
            var length = ReadUInt32BE(png, pos);
            var type = Encoding.ASCII.GetString(png, pos + 4, 4);
            if (type == "IEND") return pos;
            pos += 8 + (int)length + 4;
        }
        return -1;
    }

    private static uint ReadUInt32BE(byte[] data, int offset) =>
        (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

    private static void WriteUInt32BE(Stream s, uint value)
    {
        s.WriteByte((byte)(value >> 24));
        s.WriteByte((byte)(value >> 16));
        s.WriteByte((byte)(value >> 8));
        s.WriteByte((byte)value);
    }

    private static byte[] Concat(byte[] a, byte[] b)
    {
        var r = new byte[a.Length + b.Length];
        Buffer.BlockCopy(a, 0, r, 0, a.Length);
        Buffer.BlockCopy(b, 0, r, a.Length, b.Length);
        return r;
    }

    // CRC32 (IEEE 802.3) wymagany przez format PNG
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

    private static uint ComputeCrc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
            crc = Crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFFu;
    }
}
