namespace Tinycast.Features.Ai;

/// <summary>
/// A single OpenCode model reference: provider/model.
/// Mirrors flattenOpenCodeModels() slug emission in
/// apps/server/src/provider/Layers/OpenCodeProvider.ts.
/// </summary>
public sealed record AiModelRef(
    string ProviderId,
    string ModelId,
    string DisplayName,
    string? SubProvider = null,
    bool IsCustom = false)
{
    public string Slug => ProviderId + "/" + ModelId;

    public static AiModelRef? ParseSlug(string? slug, string displayName = "", string? subProvider = null)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;
        var trimmed = slug.Trim();
        var separator = trimmed.IndexOf('/');
        if (separator <= 0 || separator == trimmed.Length - 1)
            return null;
        return new AiModelRef(
            trimmed[..separator],
            trimmed[(separator + 1)..],
            string.IsNullOrWhiteSpace(displayName) ? trimmed : displayName.Trim(),
            string.IsNullOrWhiteSpace(subProvider) ? null : subProvider.Trim());
    }
}
