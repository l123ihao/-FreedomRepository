using System.IO;
using FormatConverter.Core.Converters;
using FormatConverter.Core.Engine;
using FormatConverter.Core.Formats;
using FormatConverter.Core.Models;

namespace FormatConverter.App.Services;

/// <summary>
/// 命令行静默转换(--convert &lt;target&gt; &lt;files...&gt;):
/// 输出到源文件同目录,重名自动加序号;不兼容文件忽略;返回进程退出码。
/// </summary>
public static class CommandLineConverter
{
    public static async Task<int> RunAsync(string targetExtension, IReadOnlyList<string> files)
    {
        var targetExt = targetExtension.TrimStart('.').ToLowerInvariant();
        var engine = new ConversionEngine(ConverterFactory.CreateDefault());

        var jobs = new List<ConversionJob>();
        var skipped = 0;

        foreach (var file in files)
        {
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

            var canConvert = FormatRegistry.GetTargets(ext)
                .Any(t => string.Equals(t.Extension, targetExt, StringComparison.OrdinalIgnoreCase));
            if (!canConvert)
            {
                skipped++;
                continue;
            }

            var output = OutputPathHelper.Resolve(file, targetExt, "", outputToSourceFolder: true, autoRename: true);
            jobs.Add(new ConversionJob(Guid.NewGuid(), file, output, targetExt,
                new ConversionOptions { OverwritePolicy = OverwritePolicy.Rename }));
        }

        if (jobs.Count == 0)
        {
            Console.WriteLine($"万能格式转换器: 没有可转换的文件(目标 {targetExt},忽略 {skipped} 个)。");
            return 1;
        }

        var results = await engine.ConvertAllAsync(jobs, progress: null, CancellationToken.None);
        var ok = results.Count(r => r.Success);
        var fail = results.Count - ok;

        Console.WriteLine($"万能格式转换器: {ok} 个成功,{fail} 个失败" +
                          (skipped > 0 ? $",忽略 {skipped} 个不兼容文件" : "") + "。");
        foreach (var r in results.Where(r => !r.Success))
            Console.WriteLine($"  失败: {Path.GetFileName(r.Job.SourcePath)} — {r.ErrorMessage}");

        return fail > 0 ? 1 : 0;
    }
}
