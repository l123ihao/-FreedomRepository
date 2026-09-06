using UglyToad.PdfPig;
using UglyToad.PdfPig.Writer;

namespace FormatConverter.Core.Tools;

/// <summary>页码范围(1 起,含两端)。</summary>
public sealed record PageRange(int Start, int End)
{
    public PageRange Normalize(int pageCount)
    {
        var start = Math.Clamp(Start, 1, pageCount);
        var end = Math.Clamp(End, start, pageCount);
        return new PageRange(start, end);
    }
}

/// <summary>PDF 工具(PdfPig 纯 .NET):合并 / 拆分 / 页数查询。</summary>
public static class PdfTools
{
    /// <summary>返回 PDF 页数;读取失败抛异常。</summary>
    public static int GetPageCount(string path)
    {
        using var doc = PdfDocument.Open(path);
        return doc.NumberOfPages;
    }

    /// <summary>按输入顺序合并多个 PDF 到单个文件。</summary>
    public static void Merge(IReadOnlyList<string> inputs, string output)
    {
        if (inputs.Count == 0) throw new ArgumentException("至少需要一个输入 PDF。", nameof(inputs));

        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var builder = new PdfDocumentBuilder();
        var opened = new List<PdfDocument>();

        try
        {
            foreach (var path in inputs)
            {
                var doc = PdfDocument.Open(path);
                opened.Add(doc);
                for (var i = 1; i <= doc.NumberOfPages; i++)
                    builder.AddPage(doc, i);
            }

            File.WriteAllBytes(output, builder.Build());
        }
        finally
        {
            foreach (var doc in opened) doc.Dispose();
        }
    }

    /// <summary>按页码范围拆分为多个 PDF,返回输出文件路径列表。</summary>
    public static IReadOnlyList<string> SplitByRanges(
        string input, string outputDir, IReadOnlyList<PageRange> ranges)
    {
        Directory.CreateDirectory(outputDir);
        var baseName = Path.GetFileNameWithoutExtension(input);
        var results = new List<string>();

        using var doc = PdfDocument.Open(input);
        var total = doc.NumberOfPages;

        for (var i = 0; i < ranges.Count; i++)
        {
            var range = ranges[i].Normalize(total);
            var path = Path.Combine(outputDir, $"{baseName}_p{range.Start}-{range.End}.pdf");

            var builder = new PdfDocumentBuilder();
            for (var page = range.Start; page <= range.End; page++)
                builder.AddPage(doc, page);
            File.WriteAllBytes(path, builder.Build());
            results.Add(path);
        }
        return results;
    }

    /// <summary>每 pagesPerFile 页拆成一个 PDF,返回输出文件路径列表。</summary>
    public static IReadOnlyList<string> SplitEveryN(string input, string outputDir, int pagesPerFile)
    {
        if (pagesPerFile < 1) throw new ArgumentException("每份页数至少为 1。", nameof(pagesPerFile));
        Directory.CreateDirectory(outputDir);

        var baseName = Path.GetFileNameWithoutExtension(input);
        using var doc = PdfDocument.Open(input);
        var total = doc.NumberOfPages;
        var results = new List<string>();

        for (var start = 1; start <= total; start += pagesPerFile)
        {
            var end = Math.Min(start + pagesPerFile - 1, total);
            var path = Path.Combine(outputDir, $"{baseName}_p{start}-{end}.pdf");

            var builder = new PdfDocumentBuilder();
            for (var page = start; page <= end; page++)
                builder.AddPage(doc, page);
            File.WriteAllBytes(path, builder.Build());
            results.Add(path);
        }
        return results;
    }
}
