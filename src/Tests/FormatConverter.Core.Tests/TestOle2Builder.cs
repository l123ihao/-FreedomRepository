using System.Text;

namespace FormatConverter.Core.Tests;

/// <summary>
/// 测试用最小 OLE2 构建器:所有流(≤3 个)走 mini stream(< 4096 字节)。
/// 布局:0=头 / 1=FAT / 2=目录 / 3=miniFAT / 4..=根目录流(承载 mini stream)。
/// FAT 链路径由真实样本文件覆盖。
/// </summary>
internal static class TestOle2Builder
{
    private const int SectorSize = 512;

    public static byte[] Build(params (string Name, byte[] Data)[] streams)
    {
        if (streams.Length > 3)
            throw new ArgumentException("本构建器最多支持 3 个流。");

        // ---- mini stream 布局:各流 64B 对齐 ----
        var miniStream = new List<byte>();
        var miniStarts = new List<int>();
        foreach (var (_, data) in streams)
        {
            miniStarts.Add(miniStream.Count);
            miniStream.AddRange(data);
            while (miniStream.Count % 64 != 0) miniStream.Add(0);
        }

        // ---- 扇区数:头 + FAT + 目录 + miniFAT + 根数据 ----
        var rootSectorCount = Math.Max(1, (miniStream.Count + SectorSize - 1) / SectorSize);
        var totalSectors = 4 + rootSectorCount;

        // ---- 头(512B) ----
        var header = new byte[512];
        WriteMagic(header, 0, 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1);
        WriteU16(header, 0x1E, 9);   // 扇区 512
        WriteU16(header, 0x20, 6);   // mini 扇区 64
        WriteU32(header, 0x2C, 1);   // FAT 扇区数
        WriteU32(header, 0x30, 2);   // 首目录扇区
        WriteU32(header, 0x38, 4096); // mini stream 阈值
        WriteU32(header, 0x3C, 3);   // 首 miniFAT 扇区
        WriteU32(header, 0x40, 1);   // miniFAT 扇区数
        WriteU32(header, 0x44, 0xFFFFFFFE); // 无 DIFAT 扇区
        WriteU32(header, 0x48, 0);
        WriteU32(header, 0x4C, 1);   // DIFAT[0] = FAT 扇区 1
        for (var i = 1; i < 109; i++) WriteU32(header, 0x4C + i * 4, 0xFFFFFFFF);

        // ---- FAT(1 扇区,128 项) ----
        var fat = new byte[SectorSize];
        for (var i = 0; i < 128; i++) WriteU32(fat, i * 4, 0xFFFFFFFF);
        WriteU32(fat, 1 * 4, 0xFFFFFFFD); // FAT 扇区自身
        WriteU32(fat, 2 * 4, 0xFFFFFFFE); // 目录链尾
        WriteU32(fat, 3 * 4, 0xFFFFFFFE); // miniFAT 链尾
        for (var i = 0; i < rootSectorCount - 1; i++)
            WriteU32(fat, (4 + i) * 4, (uint)(4 + i + 1)); // 根数据链
        WriteU32(fat, (4 + rootSectorCount - 1) * 4, 0xFFFFFFFE);

        // ---- 目录(1 扇区,4 项) ----
        var dir = new byte[SectorSize];
        WriteDirEntry(dir, 0, "Root Entry", 5, 4, miniStream.Count);
        for (var i = 0; i < streams.Length; i++)
            WriteDirEntry(dir, (i + 1) * 128, streams[i].Name, 2,
                (uint)(miniStarts[i] / 64), streams[i].Data.Length);

        // ---- miniFAT(1 扇区) ----
        var miniFat = new byte[SectorSize];
        for (var i = 0; i < 128; i++) WriteU32(miniFat, i * 4, 0xFFFFFFFF);
        var miniSectorCount = miniStream.Count / 64;
        for (var i = 0; i < miniSectorCount - 1; i++)
            WriteU32(miniFat, i * 4, (uint)(i + 1));
        if (miniSectorCount > 0)
            WriteU32(miniFat, (miniSectorCount - 1) * 4, 0xFFFFFFFE);

        // ---- 组装 ----
        var rootData = new byte[rootSectorCount * SectorSize];
        miniStream.CopyTo(rootData);
        var file = new byte[totalSectors * SectorSize];
        header.CopyTo(file, 0);
        fat.CopyTo(file, SectorSize);
        dir.CopyTo(file, 2 * SectorSize);
        miniFat.CopyTo(file, 3 * SectorSize);
        rootData.CopyTo(file, 4 * SectorSize);
        return file;
    }

    private static void WriteDirEntry(byte[] dir, int off, string name, int type, uint start, int size)
    {
        var nameBytes = Encoding.Unicode.GetBytes(name + "\0");
        nameBytes.CopyTo(dir, off);
        WriteU16(dir, off + 0x40, (ushort)nameBytes.Length); // 名称长度(字节数,含终止符)
        dir[off + 0x42] = (byte)type;
        WriteU32(dir, off + 0x74, start);
        WriteU32(dir, off + 0x78, (uint)size);
    }

    private static void WriteMagic(byte[] buf, int off, params byte[] magic)
    {
        for (var i = 0; i < magic.Length; i++) buf[off + i] = magic[i];
    }

    private static void WriteU16(byte[] buf, int off, ushort value)
    {
        buf[off] = (byte)value;
        buf[off + 1] = (byte)(value >> 8);
    }

    private static void WriteU32(byte[] buf, int off, uint value)
    {
        buf[off] = (byte)value;
        buf[off + 1] = (byte)(value >> 8);
        buf[off + 2] = (byte)(value >> 16);
        buf[off + 3] = (byte)(value >> 24);
    }
}
