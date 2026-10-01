using System.Diagnostics;
using FormatConverter.Core.Documents;
using FormatConverter.Core.Models;
using FormatConverter.Core.Pdf;
using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Converters;

/// <summary>
/// 老版二进制 Office 格式(.doc/.ppt)转换:
/// → txt:内置文本提取器(纯 .NET,无需 LibreOffice);
/// → docx/pptx/pdf/html:经 LibreOffice 无头模式(soffice --convert-to),未安装时返回带指引的友好错误。
/// soffice 并发实例会争用用户配置文件,故进程内串行。
/// </summary>
public sealed class LibreOfficeConverter : IConverter
{
    private static readonly SemaphoreSlim Gate = new(1);

    /// <summary>目标扩展名 → soffice --convert-to 过滤器(旧版 Word/PowerPoint 走本转换器)。</summary>
    private static readonly Dictionary<string, string> Filters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["docx"] = "docx",
        ["pptx"] = "pptx",
        ["pdf"] = "pdf",
        ["html"] = "html",
    };

    public bool CanConvert(ConversionJob job)
    {
        var ext = Path.GetExtension(job.SourcePath).TrimStart('.');
        return ext.Equals("doc", StringComparison.OrdinalIgnoreCase)
               || ext.Equals("ppt", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ConversionResult> ConvertAsync(
        ConversionJob job, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var tempOut = Path.Combine(Path.GetTempPath(), "fc-soffice", Guid.NewGuid().ToString("N"));
        try
        {
            progress?.Report(new ProgressInfo(0, null, null, null, 0, 0, null));
            ct.ThrowIfCancellationRequested();

            // → txt:内置提取器,无需任何外部引擎
            if (job.TargetExtension.Equals("txt", StringComparison.OrdinalIgnoreCase))
            {
                var isDoc = Path.GetExtension(job.SourcePath)
                    .TrimStart('.').Equals("doc", StringComparison.OrdinalIgnoreCase);
                var text = isDoc
                    ? DocTextExtractor.Extract(job.SourcePath)
                    : PptTextExtractor.Extract(job.SourcePath);
                Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);
                TextFile.WriteAllText(job.OutputPath, text);
                progress?.Report(new ProgressInfo(100, null, null, null, 0, 0, null));
                if (text.Length > 0) // 空文档转出空 txt 是合法结果
                    OutputValidator.EnsureNonEmpty(job.OutputPath);
                return new ConversionResult(job, true, job.OutputPath, null, sw.Elapsed);
            }

            var soffice = LibreOfficeLocator.Find();
            if (soffice is null)
            {
                // 文本模式回退:内置提取 + 纯 .NET 渲染(pdf/docx/html 可用);
                // pptx 无内置渲染器,仍需 LibreOffice
                var src = Path.GetExtension(job.SourcePath).TrimStart('.').ToUpperInvariant();
                if (job.TargetExtension.Equals("pptx", StringComparison.OrdinalIgnoreCase))
                    return new ConversionResult(job, false, null,
                        $"转换 {src}→PPTX 需要 LibreOffice(未检测到):请安装免费的 LibreOffice,或在 PowerPoint 里另存为 .pptx(转 TXT/PDF 可用内置文本模式,无需安装)。",
                        sw.Elapsed);
                return ConvertTextMode(job, progress, sw);
            }

            if (!Filters.TryGetValue(job.TargetExtension, out var filter))
                return new ConversionResult(job, false, null,
                    $"LibreOffice 不支持目标格式 {job.TargetExtension}", sw.Elapsed);

            Directory.CreateDirectory(tempOut);

            await Gate.WaitAsync(ct);
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = soffice,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
                psi.ArgumentList.Add("--headless");
                psi.ArgumentList.Add("--convert-to");
                psi.ArgumentList.Add(filter);
                psi.ArgumentList.Add("--outdir");
                psi.ArgumentList.Add(tempOut);
                psi.ArgumentList.Add(job.SourcePath);

                using var process = Process.Start(psi)!;
                try
                {
                    var stderrTask = process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync(ct);
                    var stderr = await stderrTask;

                    if (process.ExitCode != 0)
                    {
                        var tail = stderr.Split('\n').TakeLast(3);
                        return new ConversionResult(job, false, null,
                            $"LibreOffice 转换失败: {string.Join(" ", tail).Trim()}", sw.Elapsed);
                    }
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(entireProcessTree: true); } catch { /* 尽力而为 */ }
                    throw; // 取消:转为「已取消」结果
                }
            }
            finally
            {
                Gate.Release();
            }

            // soffice 输出文件名固定为「原名 + 新扩展名」,找到产物后原子改名到目标路径
            var produced = Directory.GetFiles(tempOut).FirstOrDefault();
            if (produced is null)
                return new ConversionResult(job, false, null,
                    "LibreOffice 未生成输出文件(源文件可能已损坏)。", sw.Elapsed);

            Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);
            File.Move(produced, job.OutputPath, overwrite: true);

            progress?.Report(new ProgressInfo(100, null, null, null, 0, 0, null));
            OutputValidator.EnsureNonEmpty(job.OutputPath);
            return new ConversionResult(job, true, job.OutputPath, null, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            return new ConversionResult(job, false, null, "已取消", sw.Elapsed);
        }
        catch (Exception ex)
        {
            return new ConversionResult(job, false, null, ErrorClassifier.WithCategory(ex.Message), sw.Elapsed);
        }
        finally
        {
            TryDeleteDir(tempOut);
        }
    }

    private static void TryDeleteDir(string dir)
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true); } catch { /* 尽力而为 */ }
    }

    /// <summary>
    /// 文本模式回退(未安装 LibreOffice):提取文字 → TextToModel → 纯 .NET 渲染。
    /// 仅保留文字内容(排版/图片简化),结果附 Note 提示。
    /// </summary>
    private static ConversionResult ConvertTextMode(
        ConversionJob job, IProgress<ProgressInfo>? progress, Stopwatch sw)
    {
        try
        {
            var isDoc = Path.GetExtension(job.SourcePath)
                .TrimStart('.').Equals("doc", StringComparison.OrdinalIgnoreCase);
            var text = isDoc
                ? DocTextExtractor.Extract(job.SourcePath)
                : PptTextExtractor.Extract(job.SourcePath);
            var model = TextToModel.Convert(text);
            Directory.CreateDirectory(Path.GetDirectoryName(job.OutputPath)!);

            switch (job.TargetExtension.ToLowerInvariant())
            {
                case "pdf":
                    PdfRenderer.Render(model, job.OutputPath);
                    break;
                case "docx":
                    DocxWriter.Write(model, job.OutputPath);
                    break;
                case "html":
                    TextFile.WriteAllText(job.OutputPath,
                        ModelToHtml.Convert(model, Path.GetFileNameWithoutExtension(job.SourcePath)));
                    break;
                default:
                    return new ConversionResult(job, false, null,
                        $"不支持的目标格式 {job.TargetExtension}", sw.Elapsed);
            }

            progress?.Report(new ProgressInfo(100, null, null, null, 0, 0, null));
            if (text.Length > 0)
                OutputValidator.EnsureNonEmpty(job.OutputPath);
            return new ConversionResult(job, true, job.OutputPath, null, sw.Elapsed,
                Note: "未检测到 LibreOffice,已用文本模式转换(仅保留文字,排版/图片已简化)。");
        }
        catch (Exception ex)
        {
            return new ConversionResult(job, false, null, ErrorClassifier.WithCategory(ex.Message), sw.Elapsed);
        }
    }
}
