namespace FormatConverter.Core.Models;

/// <summary>转换成功后的原文件处理动作(预设可配置)。</summary>
public enum PostConversionAction
{
    /// <summary>不执行任何动作。</summary>
    None,

    /// <summary>将原文件移入归档文件夹(归档失败不影响转换结果)。</summary>
    MoveToArchiveFolder,

    /// <summary>删除原文件(仅转换成功时执行)。</summary>
    DeleteSource,
}
