using System.Security.Cryptography;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace SDMetaKit;

/// <summary>
/// Ekstraktor metadanych z pliku obrazu PNG lub JPEG.
/// Port SdPackExifExtractor.ReadRawText — wewnętrznie używa MetadataExtractor NuGet,
/// typy z tej biblioteki nie przeciekają do publicznego API.
/// </summary>
public sealed class FileMetadataExtractor : IMetadataExtractor
{
    public async Task<RawMetadata> ExtractAsync(string filePath,
        ExtractionOptions? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? ExtractionOptions.Default;
        var bytes = await File.ReadAllBytesAsync(filePath, ct);

        string? sha256 = null;
        if (opts.ComputeSha256)
            sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        return ExtractFromBytes(bytes, sha256, opts);
    }

    public async Task<RawMetadata> ExtractAsync(Stream imageStream,
        ExtractionOptions? options = null,
        CancellationToken ct = default)
    {
        var opts = options ?? ExtractionOptions.Default;
        byte[] bytes;

        if (imageStream.CanSeek)
        {
            imageStream.Position = 0;
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }
        else
        {
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        string? sha256 = null;
        if (opts.ComputeSha256)
            sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        return ExtractFromBytes(bytes, sha256, opts);
    }

    private static RawMetadata ExtractFromBytes(byte[] bytes, string? sha256, ExtractionOptions opts)
    {
        IReadOnlyList<MetadataExtractor.Directory> directories;
        try
        {
            directories = ImageMetadataReader.ReadMetadata(new MemoryStream(bytes));
        }
        catch
        {
            return new RawMetadata { Sha256 = sha256 };
        }

        var allTags = opts.CollectAllTags
            ? new Dictionary<string, string>()
            : null;

        var candidates = new List<(string text, string kind)>();

        foreach (var dir in directories)
        {
            foreach (var tag in dir.Tags)
            {
                var value = tag.Description ?? string.Empty;
                if (allTags is not null && !string.IsNullOrWhiteSpace(value))
                    allTags[$"{dir.Name}/{tag.Name}"] = value;
            }

            if (dir.Name == "PNG-tEXt")
            {
                foreach (var tag in dir.Tags)
                {
                    if (tag.Description is null) continue;
                    if (tag.Description.StartsWith("parameters:", StringComparison.OrdinalIgnoreCase))
                    {
                        var text = tag.Description["parameters:".Length..].Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                            candidates.Add((text, "PNG-tEXt"));
                    }
                }
            }

            if (dir is ExifSubIfdDirectory subIfd)
            {
                var uc = subIfd.GetDescription(ExifDirectoryBase.TagUserComment);
                if (!string.IsNullOrWhiteSpace(uc))
                    candidates.Add((uc, "EXIF-UserComment"));
            }

            if (dir is ExifIfd0Directory ifd0)
            {
                var desc = ifd0.GetDescription(ExifDirectoryBase.TagImageDescription);
                if (!string.IsNullOrWhiteSpace(desc))
                    candidates.Add((desc, "EXIF-ImageDescription"));
            }
        }

        var chosen = SelectBestCandidate(candidates, opts);

        return new RawMetadata
        {
            ParametersText = chosen?.text,
            SourceKind = chosen?.kind ?? "Unknown",
            Sha256 = sha256,
            AllTags = (IReadOnlyDictionary<string, string>?)allTags
                ?? new Dictionary<string, string>()
        };
    }

    private static (string text, string kind)? SelectBestCandidate(
        List<(string text, string kind)> candidates, ExtractionOptions opts)
    {
        if (candidates.Count == 0) return null;

        IEnumerable<(string text, string kind)> filtered = opts.MinimumTextLength > 0
            ? candidates.Where(c => c.text.Length >= opts.MinimumTextLength)
            : candidates;

        if (opts.PreferStepsCandidate)
        {
            var withSteps = filtered.FirstOrDefault(c =>
                c.text.Contains("Steps:", StringComparison.OrdinalIgnoreCase));
            if (withSteps.text is not null) return withSteps;
        }

        return filtered.FirstOrDefault();
    }
}
