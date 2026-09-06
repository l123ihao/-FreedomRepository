namespace FormatConverter.Core.Tools;

/// <summary>
/// 格式魔数检测:读取文件头签名判断真实格式,不受扩展名误导。
/// 返回小写扩展名(如 "png");识别不出返回 null。
/// </summary>
public static class FormatDetector
{
    /// <summary>按文件路径检测(读前 64 字节)。读取失败返回 null。</summary>
    public static string? DetectFile(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> header = stackalloc byte[64];
            var n = fs.Read(header);
            return Detect(header[..n]);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>按文件头字节检测真实格式。</summary>
    public static string? Detect(ReadOnlySpan<byte> h)
    {
        if (h.Length < 4) return null;

        // ---- 图片 ----
        if (StartsWith(h, 0x89, 0x50, 0x4E, 0x47)) return "png";              // PNG
        if (StartsWith(h, 0xFF, 0xD8, 0xFF)) return "jpg";                    // JPEG
        if (StartsWith(h, 0x47, 0x49, 0x46, 0x38)) return "gif";              // GIF87a/89a
        if (StartsWith(h, 0x42, 0x4D)) return "bmp";                          // BMP
        if (h.Length >= 4 && h[0] == 0x00 && h[1] == 0x00 && h[2] == 0x01 && h[3] == 0x00) return "ico";
        if (Riff(h, "WEBP")) return "webp";                                   // RIFF....WEBP

        // ---- 文档/容器 ----
        if (StartsWith(h, 0x25, 0x50, 0x44, 0x46)) return "pdf";              // %PDF
        if (h.Length >= 4 && h[0] == (byte)'P' && h[1] == (byte)'K'
            && (h[2] == 0x03 || h[2] == 0x05 || h[2] == 0x07) && h[3] == 0x04)
            return "zip";                                                    // PK\x03\x04 等(docx/pptx/xlsx/zip 容器)

        // ---- 媒体 ----
        if (StartsWith(h, 0x1A, 0x45, 0xDF, 0xA3)) return "mkv";              // EBML(mkv/webm)
        if (h.Length >= 8 && h[4] == (byte)'f' && h[5] == (byte)'t'
            && h[6] == (byte)'y' && h[7] == (byte)'p') return "mp4";          // ....ftyp
        if (StartsWith(h, 0x4F, 0x67, 0x67, 0x53)) return "ogg";              // OggS
        if (StartsWith(h, 0x66, 0x4C, 0x61, 0x43)) return "flac";             // fLaC
        if (StartsWith(h, 0x49, 0x44, 0x33)) return "mp3";                    // ID3
        if (h[0] == 0xFF && (h[1] & 0xE0) == 0xE0) return "mp3";              // MPEG 音频帧
        if (Riff(h, "AVI ")) return "avi";                                    // RIFF....AVI
        if (Riff(h, "WAVE")) return "wav";                                    // RIFF....WAVE

        return null;
    }

    /// <summary>扩展名是否为已知魔数格式(用于 UI 提示)。</summary>
    public static bool IsKnownExtension(string extension) =>
        KnownFormats.Contains(extension.TrimStart('.'), StringComparer.OrdinalIgnoreCase);

    public static readonly HashSet<string> KnownFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "gif", "bmp", "ico", "webp",
        "pdf", "zip", "docx", "pptx", "xlsx",
        "mkv", "webm", "mp4", "mov", "avi", "wav", "mp3", "flac", "ogg",
    };

    private static bool StartsWith(ReadOnlySpan<byte> h, params byte[] sig)
    {
        if (h.Length < sig.Length) return false;
        for (var i = 0; i < sig.Length; i++)
            if (h[i] != sig[i]) return false;
        return true;
    }

    private static bool Riff(ReadOnlySpan<byte> h, string fourCc)
    {
        if (h.Length < 12) return false;
        if (h[0] != (byte)'R' || h[1] != (byte)'I' || h[2] != (byte)'F' || h[3] != (byte)'F') return false;
        return h[8] == (byte)fourCc[0] && h[9] == (byte)fourCc[1]
               && h[10] == (byte)fourCc[2] && h[11] == (byte)fourCc[3];
    }
}
