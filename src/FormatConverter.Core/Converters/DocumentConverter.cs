using System.Diagnostics;
using FormatConverter.Core.Documents;
using FormatConverter.Core.Markdown;
using FormatConverter.Core.Models;
using FormatConverter.Core.Pdf;
using FormatConverter.Core.Tools;

namespace FormatConverter.Core.Converters;

/// <summary>文档转换总入口:docx/txt/md/pdf 之间矩阵的路由。</summary>
public sealed class DocumentConverter : IConverter
{
    public bool CanConvert(ConversionJob job) => job.Category == FileCategory.Document;

    public async Task<ConversionResult> ConvertAsync(
        ConversionJob job, IProgress<ProgressInfo>? progress, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            progress?.Report(new ProgressInfo(0, null, null, null, 0, 0, null));
            ct.ThrowIfCancellationRequested();

            var src = Path.GetExtension(job.SourcePath).TrimStart('.').ToLowerInvariant();
            var dst = job.TargetExtension.ToLowerInvariant();

            var dir = Path.GetDirectoryName(job.OutputPath)!;
            Directory.CreateDirectory(dir);

            if (src == "pdf" && dst == "txt")
            {
                await Task.Run(() => PdfToTxtConverter.Convert(job.SourcePath, job.OutputPath), ct);
            }
            else
            {
                var text = src is "md" or "txt" ? TextFile.ReadAllText(job.SourcePath) : null;
                var model = src switch
                {
                    "docx" => DocxReader.Read(job.SourcePath),
                    "pptx" => PptxReader.Read(job.SourcePath),
                    "md" => MarkdownToModel.Convert(text!),
                    "txt" => TextToModel.Convert(text!),
                    _ => throw new NotSupportedException($"不支持从 {src} 格式转换"),
                };

                switch (dst)
                {
                    case "txt":
                        TextFile.WriteAllText(job.OutputPath, ModelToTxt.Convert(model));
                        break;
                    case "md":
                        TextFile.WriteAllText(job.OutputPath, ModelToMarkdown.Convert(model));
                        break;
                    case "html":
                        var html = src == "md"
                            ? MarkdownToHtml.Convert(text!) // md 用 Markdig 原生渲染,保真度最高
                            : ModelToHtml.Convert(model, Path.GetFileNameWithoutExtension(job.SourcePath));
                        TextFile.WriteAllText(job.OutputPath, html);
                        break;
                    case "docx":
                        DocxWriter.Write(model, job.OutputPath);
                        break;
                    case "pdf":
                        PdfRenderer.Render(model, job.OutputPath);
                        break;
                    default:
                        throw new NotSupportedException($"不支持的目标文档格式: {dst}");
                }
            }

            progress?.Report(new ProgressInfo(100, null, null, null, 0, 0, null));
            OutputValidator.EnsureNonEmpty(job.OutputPath);
            return new ConversionResult(job, true, job.OutputPath, null, sw.Elapsed);
        }
        catch (OperationCanceledException)
        {
            TryDelete(job.OutputPath);
            return new ConversionResult(job, false, null, "已取消", sw.Elapsed);
        }
        catch (Exception ex)
        {
            TryDelete(job.OutputPath);
            return new ConversionResult(job, false, null, ErrorClassifier.WithCategory(ex.Message), sw.Elapsed);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* 尽力而为 */ }
    }
}
