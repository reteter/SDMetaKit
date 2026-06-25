namespace SDMetaKit;

/// <summary>
/// Pomocnicze metody do wyświetlania SdMetadata w UI.
/// Zastępuje iterowanie po PriorityParams dict z ParsedExif.
/// </summary>
public static class SdMetadataDisplay
{
    /// <summary>
    /// Zwraca (etykieta, wartość) dla znanych parametrów w kolejności wyświetlania.
    /// Pomija pola z null wartością.
    /// </summary>
    public static IEnumerable<(string Label, string Value)> PriorityFields(SdMetadata m)
    {
        if (m.Steps is not null)               yield return ("Steps", m.Steps);
        if (m.Sampler is not null)             yield return ("Sampler", m.Sampler);
        if (m.ScheduleType is not null)        yield return ("Schedule type", m.ScheduleType);
        if (m.CfgScale is not null)            yield return ("CFG scale", m.CfgScale);
        if (m.DistilledCfgScale is not null)   yield return ("Distilled CFG Scale", m.DistilledCfgScale);
        if (m.Seed is not null)                yield return ("Seed", m.Seed);
        if (m.Size is not null)                yield return ("Size", m.Size);
        if (m.Model is not null)               yield return ("Model", m.Model);
        if (m.ModelHash is not null)           yield return ("Model hash", m.ModelHash);
        if (m.DenoisingStrength is not null)   yield return ("Denoising strength", m.DenoisingStrength);
        if (m.HiresUpscale is not null)        yield return ("Hires upscale", m.HiresUpscale);
        if (m.HiresSteps is not null)          yield return ("Hires steps", m.HiresSteps);
        if (m.HiresUpscaler is not null)       yield return ("Hires upscaler", m.HiresUpscaler);
        if (m.HiresCfgScale is not null)       yield return ("Hires CFG Scale", m.HiresCfgScale);
        if (m.HiresModule1 is not null)        yield return ("Hires Module 1", m.HiresModule1);
    }
}
