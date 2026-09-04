using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace FormatConverter.Core.Images;

/// <summary>
/// ICO 读写(ImageSharp 3.1.x 不支持 ico,4.x 才支持但需 license key,故手写)。
/// 现代 Windows 图标(Vista+)即 PNG 压缩条目:ICONDIR 头 + 目录项 + PNG 数据。
/// 解码取字节数最大的条目(通常分辨率最高),PNG/BMP 条目统一交给 ImageSharp。
/// </summary>
public static class IcoCodec
{
    private const int MaxSize = 256; // ICO 规格上限

    public static Image Decode(byte[] data)
    {
        if (data.Length < 6)
            throw new InvalidDataException("ICO 文件过小");
        if (ReadU16(data, 2) != 1 || ReadU16(data, 4) == 0)
            throw new InvalidDataException("不是有效的 ICO 文件");

        var count = ReadU16(data, 4);
        var bestOffset = 0;
        var bestSize = 0;
        for (var i = 0; i < count; i++)
        {
            var entry = 6 + i * 16;
            if (entry + 16 > data.Length)
                throw new InvalidDataException("ICO 目录损坏");
            var size = ReadU32(data, entry + 8);
            var offset = ReadU32(data, entry + 12);
            if ((long)offset + size > data.Length) continue; // 跳过损坏条目
            if (size > bestSize) { bestSize = (int)size; bestOffset = (int)offset; }
        }
        if (bestSize == 0)
            throw new InvalidDataException("ICO 中没有可用图像");

        // PNG 与 BMP 条目 ImageSharp 都能按魔数自动识别
        return Image.Load(data.AsSpan(bestOffset, bestSize));
    }

    /// <summary>编码为单 PNG 条目的 .ico 文件(超出 256px 先等比缩小)。</summary>
    public static byte[] Encode(Image image)
    {
        using var ms = new MemoryStream();
        if (image.Width > MaxSize || image.Height > MaxSize)
        {
            using var resized = image.Clone(x => x.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(MaxSize, MaxSize),
                Sampler = KnownResamplers.Lanczos3,
            }));
            resized.Save(ms, new PngEncoder());
        }
        else
        {
            image.Save(ms, new PngEncoder());
        }
        var png = ms.ToArray();

        // 取实际(可能已缩小的)尺寸
        var w = Math.Min(image.Width, MaxSize);
        var h = Math.Min(image.Height, MaxSize);

        var header = new byte[22];
        WriteU16(header, 0, 0);                 // reserved
        WriteU16(header, 2, 1);                 // type: 1 = icon
        WriteU16(header, 4, 1);                 // 1 张图像
        header[6] = w == MaxSize ? (byte)0 : (byte)w;  // 0 表示 256
        header[7] = h == MaxSize ? (byte)0 : (byte)h;
        header[8] = 0;                          // 调色板色数
        header[9] = 0;                          // reserved
        WriteU16(header, 10, 1);                // planes
        WriteU16(header, 12, 32);               // bitcount(PNG 条目忽略)
        WriteU32(header, 14, (uint)png.Length); // 图像数据大小
        WriteU32(header, 18, 22);               // 数据偏移(紧跟 6 字节头 + 16 字节目录项)

        return header.Concat(png).ToArray();
    }

    private static ushort ReadU16(byte[] b, int offset) =>
        (ushort)(b[offset] | (b[offset + 1] << 8));

    private static uint ReadU32(byte[] b, int offset) =>
        (uint)(b[offset] | (b[offset + 1] << 8) | (b[offset + 2] << 16) | (b[offset + 3] << 24));

    private static void WriteU16(byte[] b, int offset, ushort v)
    {
        b[offset] = (byte)v;
        b[offset + 1] = (byte)(v >> 8);
    }

    private static void WriteU32(byte[] b, int offset, uint v)
    {
        b[offset] = (byte)v;
        b[offset + 1] = (byte)(v >> 8);
        b[offset + 2] = (byte)(v >> 16);
        b[offset + 3] = (byte)(v >> 24);
    }
}
