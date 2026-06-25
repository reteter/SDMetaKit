using System.Text.RegularExpressions;

namespace SDMetaKit;

/// <summary>
/// Parser formatu A1111/Forge: prompt \n Negative prompt: \n Steps: klucz: wartość, ...
/// Unifikacja ExifParser (SDInfo) i SdPackExifExtractor (SDPack) — identyczna logika, jeden kod.
/// </summary>
public sealed class A1111Parser : IMetadataParser
{
    private static readonly HashSet<string> KnownPriorityKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Steps", "Sampler", "Schedule type", "CFG scale", "Distilled CFG Scale",
        "Seed", "Size", "Model", "Model hash", "Denoising strength",
        "Hires upscale", "Hires steps", "Hires upscaler", "Hires CFG Scale", "Hires Module 1"
    };

    public SdMetadata Parse(string parametersText)
    {
        if (string.IsNullOrWhiteSpace(parametersText))
            return new SdMetadata { SourceKind = "Text", RawText = parametersText };

        var negMatch = Regex.Match(parametersText, @"(?m)^Negative prompt:\s*", RegexOptions.Multiline);

        var searchFrom = negMatch.Success ? negMatch.Index + negMatch.Length : 0;
        var stepsMatch = Regex.Match(parametersText[searchFrom..], @"(?m)^Steps:", RegexOptions.Multiline);
        var stepsIndex = stepsMatch.Success ? searchFrom + stepsMatch.Index : -1;

        int promptEnd, negEnd, paramsStart;

        if (negMatch.Success)
        {
            promptEnd = negMatch.Index;
            negEnd = negMatch.Index + negMatch.Length;
            paramsStart = stepsIndex;
        }
        else if (stepsIndex >= 0)
        {
            promptEnd = stepsIndex;
            negEnd = -1;
            paramsStart = stepsIndex;
        }
        else
        {
            return new SdMetadata
            {
                Prompt = parametersText.Trim(),
                SourceKind = "Text",
                RawText = parametersText
            };
        }

        var prompt = parametersText[..promptEnd].Trim();
        var negativePrompt = "";

        if (negEnd >= 0 && paramsStart > negEnd)
            negativePrompt = parametersText[negEnd..paramsStart].Trim();
        else if (negEnd >= 0 && paramsStart < 0)
            negativePrompt = parametersText[negEnd..].Trim();

        if (paramsStart < 0)
        {
            return new SdMetadata
            {
                Prompt = prompt,
                NegativePrompt = negativePrompt,
                SourceKind = "Text",
                RawText = parametersText
            };
        }

        var allParams = ParseKeyValue(parametersText[paramsStart..]);
        var other = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var kv in allParams)
        {
            if (!KnownPriorityKeys.Contains(kv.Key))
                other[kv.Key] = kv.Value;
        }

        return new SdMetadata
        {
            Prompt = prompt,
            NegativePrompt = negativePrompt,
            Steps = Get(allParams, "Steps"),
            Sampler = Get(allParams, "Sampler"),
            ScheduleType = Get(allParams, "Schedule type"),
            CfgScale = Get(allParams, "CFG scale"),
            DistilledCfgScale = Get(allParams, "Distilled CFG Scale"),
            Seed = Get(allParams, "Seed"),
            Size = Get(allParams, "Size"),
            Model = Get(allParams, "Model"),
            ModelHash = Get(allParams, "Model hash"),
            DenoisingStrength = Get(allParams, "Denoising strength"),
            HiresUpscale = Get(allParams, "Hires upscale"),
            HiresSteps = Get(allParams, "Hires steps"),
            HiresUpscaler = Get(allParams, "Hires upscaler"),
            HiresCfgScale = Get(allParams, "Hires CFG Scale"),
            HiresModule1 = Get(allParams, "Hires Module 1"),
            OtherParams = other,
            SourceKind = "Text",
            RawText = parametersText
        };
    }

    private static string? Get(Dictionary<string, string> d, string key)
        => d.TryGetValue(key, out var v) ? v : null;

    internal static Dictionary<string, string> ParseKeyValue(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = 0, len = input.Length;

        while (i < len)
        {
            while (i < len && (input[i] == ',' || input[i] == '\n' || input[i] == '\r' || input[i] == ' '))
                i++;
            if (i >= len) break;

            int keyStart = i;
            while (i < len && input[i] != ':' && input[i] != '\n')
                i++;

            if (i >= len) break;
            if (input[i] == '\n') { i++; continue; }

            string key = input[keyStart..i].Trim();
            i++;
            if (i < len && input[i] == ' ') i++;

            string value;
            if (i < len && input[i] == '"')
            {
                i++;
                var sb = new System.Text.StringBuilder();
                while (i < len)
                {
                    if (input[i] == '\\' && i + 1 < len && input[i + 1] == '"') { sb.Append('"'); i += 2; }
                    else if (input[i] == '"') { i++; break; }
                    else sb.Append(input[i++]);
                }
                value = sb.ToString().Trim();
            }
            else
            {
                int valStart = i;
                while (i < len && input[i] != ',' && input[i] != '\n')
                    i++;
                value = input[valStart..i].Trim();
            }

            if (!string.IsNullOrEmpty(key))
                result[key] = value;
        }

        return result;
    }
}
