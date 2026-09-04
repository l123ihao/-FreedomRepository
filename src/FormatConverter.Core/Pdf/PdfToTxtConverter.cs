using System.Text;
using FormatConverter.Core.Documents;
using UglyToad.PdfPig;

namespace FormatConverter.Core.Pdf;

/// <summary>pdf → txt(PdfPig 提取每页文本)。</summary>
public static class PdfToTxtConverter
{
    public static void Convert(string inputPath, string outputPath)
    {
        using var pdf = PdfDocument.Open(inputPath);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);
        TextFile.WriteAllText(outputPath, sb.ToString());
    }
}
