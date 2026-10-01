using FormatConverter.Core.Models;

namespace FormatConverter.Core.Presets;

/// <summary>
/// 内置预设与用户预设合并(File Converter Merge 思路的 JSON 化):
/// 顺序 = PresetOrder 中的有效 Id(按序)→ 未列入的用户预设(存储序)→ 未列入且未删除的内置(默认序)。
/// </summary>
public static class PresetMerger
{
    public static IReadOnlyList<ConversionPreset> Merge(
        IReadOnlyList<ConversionPreset> builtIns,
        IReadOnlyList<ConversionPreset> userPresets,
        IReadOnlyList<string>? order,
        IReadOnlyList<string>? deletedBuiltInIds)
    {
        var byId = new Dictionary<string, ConversionPreset>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in builtIns) byId[p.Id] = p;
        foreach (var p in userPresets) byId[p.Id] = p; // 同 Id 时用户预设优先

        var deleted = new HashSet<string>(deletedBuiltInIds ?? [], StringComparer.OrdinalIgnoreCase);
        var result = new List<ConversionPreset>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in order ?? [])
        {
            if (used.Contains(id) || deleted.Contains(id)) continue;
            if (byId.TryGetValue(id, out var preset))
            {
                result.Add(preset);
                used.Add(id);
            }
        }

        foreach (var p in userPresets)
            if (used.Add(p.Id)) result.Add(p);

        foreach (var p in builtIns)
            if (!deleted.Contains(p.Id) && used.Add(p.Id)) result.Add(p);

        return result;
    }

    /// <summary>按 Id 精确匹配,再按名称忽略大小写匹配;均不中返回 null。</summary>
    public static ConversionPreset? Resolve(string idOrName, IReadOnlyList<ConversionPreset> effective)
    {
        foreach (var p in effective)
            if (string.Equals(p.Id, idOrName, StringComparison.OrdinalIgnoreCase)) return p;
        foreach (var p in effective)
            if (string.Equals(p.Name, idOrName, StringComparison.OrdinalIgnoreCase)) return p;
        return null;
    }
}
