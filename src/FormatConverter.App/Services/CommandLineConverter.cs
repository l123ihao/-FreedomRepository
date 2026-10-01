using System.IO;
using FormatConverter.Core.Converters;
using FormatConverter.Core.Engine;
using FormatConverter.Core.Formats;
using FormatConverter.Core.Models;
using FormatConverter.Core.Presets;
using FormatConverter.Core.Tools;

namespace FormatConverter.App.Services;

/// <summary>
/// 命令行静默转换:
/// --convert &lt;扩展名|预设名&gt; &lt;files...&gt;(兼容旧语法;选项/输出目录/重名策略跟随持久化设置)
/// --preset &lt;预设名|Id&gt; &lt;files...&gt;(按预设:模板命名 + 参数覆盖 + 转换后动作)
/// 返回进程退出码 0/1。
/// </summary>
public static class CommandLineConverter
{
    public static async Task<int> RunAsync(string targetOrPreset, IReadOnlyList<string> files, bool presetOnly)
    {
        var settings = SettingsService.Load();
        var effective = PresetMerger.Merge(
            BuiltInPresets.Defaults, settings.UserPresets,
            settings.PresetOrder, settings.DeletedBuiltInPresetIds);

        // 先按扩展名解析(--preset 模式跳过),再按预设名/Id;歧义由预设名校验(禁止=扩展名)兜底
        var targetExt = "";
        if (!presetOnly)
        {
            var ext = targetOrPreset.TrimStart('.').ToLowerInvariant();
            if (FormatRegistry.IsTargetFormat(ext)) targetExt = ext;
        }
        var preset = targetExt.Length == 0 ? PresetMerger.Resolve(targetOrPreset, effective) : null;

        if (targetExt.Length == 0 && preset is null)
        {
            Console.WriteLine($"万能格式转换器: 未知的目标格式或预设 \"{targetOrPreset}\"。");
            Console.WriteLine("用法: FormatConverter.exe --convert <目标扩展名> <文件...>");
            Console.WriteLine("      FormatConverter.exe --preset <预设名> <文件...>");
            return 1;
        }

        var engine = new ConversionEngine(ConverterFactory.CreateDefault());
        var jobs = new List<ConversionJob>();
        var batchBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skipped = 0;

        for (var i = 0; i < files.Count; i++)
        {
            // 统一转绝对路径(模板 (p)/源目录推导依赖绝对路径;右键菜单传的 "%1" 已是绝对路径)
            var file = Path.GetFullPath(files[i]);
            if (!File.Exists(file))
            {
                skipped++;
                continue;
            }
            var ext = Path.GetExtension(file).TrimStart('.');
            if (!FormatRegistry.IsSupported(ext))
            {
                skipped++;
                continue;
            }

            if (targetExt.Length > 0)
            {
                // --convert <ext> 兼容路径:输出目录/重名策略/参数跟随持久化设置
                if (!FormatRegistry.GetTargets(ext).Any(t =>
                        string.Equals(t.Extension, targetExt, StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;
                    continue;
                }
                var output = OutputPathResolver.ResolveLegacy(file, targetExt,
                    settings.OutputDirectory, settings.OutputToSourceFolder, settings.AutoRename, batchBlacklist);
                batchBlacklist.Add(output);
                jobs.Add(new ConversionJob(Guid.NewGuid(), file, output, targetExt,
                    settings.ToConversionOptions()));
            }
            else
            {
                // --preset 路径:模板命名 + 参数覆盖 + 转换后动作
                var p = preset!;
                if (!FormatRegistry.GetTargets(ext).Any(t =>
                        string.Equals(t.Extension, p.TargetExtension, StringComparison.OrdinalIgnoreCase)))
                {
                    skipped++;
                    continue;
                }
                string output;
                try
                {
                    var ctx = new TemplateContext(file, p.TargetExtension, i + 1, files.Count, DateTime.Now);
                    output = OutputPathResolver.Resolve(
                        file, p.TargetExtension, p.OutputFileNameTemplate,
                        settings.OutputDirectory, settings.OutputToSourceFolder, settings.AutoRename,
                        ctx, batchBlacklist);
                }
                catch (OutputTemplateException ex)
                {
                    Console.WriteLine($"万能格式转换器: 跳过 {Path.GetFileName(file)} — {ex.Message}");
                    skipped++;
                    continue;
                }
                batchBlacklist.Add(output);
                var options = p.Options?.ApplyTo(settings.ToConversionOptions()) ?? settings.ToConversionOptions();
                jobs.Add(new ConversionJob(Guid.NewGuid(), file, output, p.TargetExtension,
                    options, p.PostConversionAction, p.ArchiveFolder));
            }
        }

        if (jobs.Count == 0)
        {
            Console.WriteLine($"万能格式转换器: 没有可转换的文件(目标 {targetOrPreset},忽略 {skipped} 个)。");
            return 1;
        }

        var results = await engine.ConvertAllAsync(jobs, progress: null, CancellationToken.None);
        var ok = results.Count(r => r.Success);
        var fail = results.Count - ok;

        Console.WriteLine($"万能格式转换器: {ok} 个成功,{fail} 个失败" +
                          (skipped > 0 ? $",忽略 {skipped} 个不兼容文件" : "") + "。");
        foreach (var r in results.Where(r => !r.Success))
            Console.WriteLine($"  失败: {Path.GetFileName(r.Job.SourcePath)} — {r.ErrorMessage}");
        foreach (var r in results.Where(r => r.Success && r.PostAction is not null))
        {
            var detail = r.PostAction!.Detail;
            if (detail.Length > 0)
                Console.WriteLine($"  {Path.GetFileName(r.Job.SourcePath)}:{detail}");
        }
        foreach (var r in results.Where(r => r.Success && r.Note is not null))
            Console.WriteLine($"  提示:{Path.GetFileName(r.Job.SourcePath)} — {r.Note}");

        return fail > 0 ? 1 : 0;
    }
}
