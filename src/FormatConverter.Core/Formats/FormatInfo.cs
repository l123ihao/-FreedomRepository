using FormatConverter.Core.Models;

namespace FormatConverter.Core.Formats;

/// <summary>一种受支持的文件格式。</summary>
/// <param name="Extension">小写扩展名,不含点。</param>
/// <param name="Category">所属大类。</param>
/// <param name="DisplayName">界面显示名。</param>
public sealed record FormatInfo(string Extension, FileCategory Category, string DisplayName)
{
    public override string ToString() => $"{DisplayName} (*.{Extension})";
}
