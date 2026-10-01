using System.Text;

namespace FormatConverter.Core.Documents;

/// <summary>
/// 最小 OLE2 复合文档读取器(只读流内容):用于解析老版二进制 Office(.doc/.ppt)。
/// 结构:512B 头(含 DIFAT)→ FAT 扇区链 → 目录扇区 → 数据扇区;
/// 小于 4096 字节的流存于 mini stream(根目录流内,64B mini 扇区 + miniFAT 链)。
/// 解析思路对照 NPOI POIFS / LibreOffice SotStorage(本项目实现为最小集)。
/// </summary>
public sealed class Ole2Reader : IDisposable
{
    private const uint FreeSect = 0xFFFFFFFF;
    private const uint EndOfChain = 0xFFFFFFFE;
    private const uint FatSect = 0xFFFFFFFD;
    private const uint DifSect = 0xFFFFFFFC;

    private readonly FileStream _fs;
    private readonly int _sectorSize;
    private readonly int _miniSectorSize;
    private readonly long _miniStreamCutoff;
    private readonly long _dataOffset;
    private readonly uint[] _fat;
    private readonly uint[] _miniFat;
    private readonly byte[] _miniStream;
    private readonly Dictionary<string, uint> _streams = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, long> _streamSizes = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>打开 OLE2 复合文档;结构非法时抛 InvalidDataException(中文消息)。</summary>
    public static Ole2Reader Open(string path) => new(path);

    private Ole2Reader(string path)
    {
        _fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        try
        {
            if (_fs.Length < 512)
                throw new InvalidDataException("不是 OLE2 复合文档(老版 .doc/.ppt 格式)。");

            Span<byte> header = stackalloc byte[512];
            _fs.ReadExactly(header);

            if (header[0] != 0xD0 || header[1] != 0xCF || header[2] != 0x11 || header[3] != 0xE0)
                throw new InvalidDataException("不是 OLE2 复合文档(老版 .doc/.ppt 格式)。");

            var sectorShift = ReadU16(header, 0x1E);
            if (sectorShift is not (9 or 12))
                throw new InvalidDataException($"不支持的扇区大小(2^{sectorShift})。");
            _sectorSize = 1 << sectorShift;
            _miniSectorSize = 1 << ReadU16(header, 0x20);
            var numFatSectors = ReadU32(header, 0x2C);
            var firstDirSector = ReadU32(header, 0x30);
            _miniStreamCutoff = ReadU32(header, 0x38);
            var firstMiniFatSector = ReadU32(header, 0x3C);
            var numMiniFatSectors = ReadU32(header, 0x40);
            var firstDifatSector = ReadU32(header, 0x44);
            var numDifatSectors = ReadU32(header, 0x48);

            var totalSectors = (uint)((_fs.Length + _sectorSize - 1) / _sectorSize);
            _fat = new uint[totalSectors];
            Array.Fill(_fat, FreeSect);

            // 数据扇区基准偏移:标准文件为 0;个别 Mac 版 Office 生成的文件整体偏移一个扇区
            // (头部字段指向的目录扇区位置找不到根目录条目时,探测 +1 扇区偏移)。
            _dataOffset = 0;
            if (!HasRootEntry(ReadRawSector(_dataOffset, firstDirSector)))
            {
                if (HasRootEntry(ReadRawSector(_sectorSize, firstDirSector)))
                    _dataOffset = _sectorSize;
                else
                    throw new InvalidDataException("目录结构无法解析,文件已损坏或格式不支持。");
            }

            // DIFAT:头 109 项 + DIFAT 扇区链
            var fatIds = new List<uint>(numFatSectors > 0 ? (int)Math.Min(numFatSectors, 109) : 0);
            for (var i = 0; i < 109 && fatIds.Count < numFatSectors; i++)
            {
                var id = ReadU32(header, 0x4C + i * 4);
                if (id != FreeSect) fatIds.Add(id);
            }
            var difat = firstDifatSector;
            for (var d = 0; d < numDifatSectors && difat != EndOfChain && difat != FreeSect; d++)
            {
                if (difat >= totalSectors) throw new InvalidDataException("DIFAT 扇区越界,文件已损坏。");
                var sector = ReadSectorBytes(difat);
                var perSector = _sectorSize / 4 - 1;
                for (var i = 0; i < perSector && fatIds.Count < numFatSectors; i++)
                {
                    var id = ReadU32(sector, i * 4);
                    if (id != FreeSect) fatIds.Add(id);
                }
                difat = ReadU32(sector, perSector * 4);
            }

            // FAT:FAT 是一张按「逻辑条目序号」排列的扁平表——第 n 个 FAT 扇区覆盖条目 n*128..n*128+127
            var fatOrdinal = 0;
            foreach (var fatSector in fatIds)
            {
                if (fatSector >= totalSectors) throw new InvalidDataException("FAT 扇区越界,文件已损坏。");
                var sector = ReadSectorBytes(fatSector);
                for (var i = 0; i < _sectorSize / 4; i++)
                {
                    var index = fatOrdinal * (_sectorSize / 4) + i;
                    if (index < _fat.Length)
                        _fat[index] = ReadU32(sector, i * 4);
                }
                fatOrdinal++;
            }

            // 目录
            var rootEntryStart = long.MaxValue;
            var rootEntrySize = 0L;
            foreach (var (name, type, start, size) in ReadDirectory(firstDirSector))
            {
                if (type == 5)
                {
                    rootEntryStart = start;
                    rootEntrySize = size;
                }
                else if (type == 2)
                {
                    _streams[name] = start;
                    _streamSizes[name] = size;
                }
            }
            if (rootEntryStart == long.MaxValue)
                throw new InvalidDataException("缺少根目录条目,文件已损坏。");

            // miniFAT + mini stream(根目录流承载)
            var miniFat = new List<uint>();
            var mini = firstMiniFatSector;
            for (var d = 0; d < numMiniFatSectors && mini != EndOfChain && mini != FreeSect; d++)
            {
                if (mini >= totalSectors) throw new InvalidDataException("miniFAT 扇区越界,文件已损坏。");
                var sector = ReadSectorBytes(mini);
                for (var i = 0; i < _sectorSize / 4; i++)
                    miniFat.Add(ReadU32(sector, i * 4));
                mini = _fat[mini];
            }
            _miniFat = miniFat.ToArray();

            var ms = new List<byte>();
            var rootSector = (uint)rootEntryStart;
            while (rootSector != EndOfChain && rootSector != FreeSect)
            {
                if (rootSector >= totalSectors) throw new InvalidDataException("根目录流越界,文件已损坏。");
                var sector = ReadSectorBytes(rootSector);
                var take = (int)Math.Min(sector.Length, rootEntrySize - ms.Count);
                for (var i = 0; i < take; i++) ms.Add(sector[i]);
                if (ms.Count >= rootEntrySize) break;
                rootSector = _fat[rootSector];
            }
            _miniStream = ms.ToArray();
        }
        catch (InvalidDataException)
        {
            _fs.Dispose();
            throw;
        }
    }

    /// <summary>按名称读流(忽略大小写);不存在返回 null。</summary>
    public byte[]? ReadStream(string name)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_streams.TryGetValue(name, out var start)) return null;
        var size = _streamSizes[name];

        if (size >= _miniStreamCutoff)
            return ReadFatChain(start, size);

        // mini stream:start 为 mini 扇区号,数据位于根目录流内
        var result = new byte[size];
        var offset = 0L;
        var mini = start;
        while (mini != EndOfChain && mini != FreeSect && offset < size)
        {
            var src = (long)mini * _miniSectorSize;
            if (src + _miniSectorSize > _miniStream.Length)
                throw new InvalidDataException("mini 流越界,文件已损坏。");
            var take = (int)Math.Min(_miniSectorSize, size - offset);
            Array.Copy(_miniStream, src, result, offset, take);
            offset += take;
            if ((long)mini >= _miniFat.Length)
                throw new InvalidDataException("miniFAT 越界,文件已损坏。");
            mini = _miniFat[mini];
        }
        return offset == size ? result : throw new InvalidDataException("mini 流不完整,文件已损坏。");
    }

    public void Dispose()
    {
        _disposed = true;
        _fs.Dispose();
    }

    // ---------- 内部 ----------

    private IEnumerable<(string Name, int Type, uint Start, long Size)> ReadDirectory(uint firstSector)
    {
        var sector = firstSector;
        var visited = 0;
        while (sector != EndOfChain && sector != FreeSect)
        {
            if (sector >= _fat.Length || ++visited > 1_000_000)
                throw new InvalidDataException($"目录扇区越界(sector={sector},fatLen={_fat.Length}),文件已损坏。");
            var data = ReadSectorBytes(sector);
            for (var off = 0; off + 128 <= data.Length; off += 128)
            {
                var nameLen = ReadU16(data, off + 0x40);
                var type = data[off + 0x42];
                if (type == 0) continue; // 空槽
                var name = nameLen >= 2 && nameLen <= 64
                    ? Encoding.Unicode.GetString(data, off, nameLen - 2)
                    : "";
                var start = ReadU32(data, off + 0x74);
                var size = (long)ReadU64(data, off + 0x78);
                if (name.Length == 0) continue;
                yield return (name, type, start, size);
            }
            sector = _fat[sector];
        }
    }

    /// <summary>
    /// 按 FAT 链读流:信任链读到 EndOfChain 为止(个别 Mac 版 Office 写入的目录 size 字段
    /// 小于实际链长,按 size 截断会丢尾部扇区;链长 < size 才判损坏),上限 256MB 防异常链。
    /// </summary>
    private byte[] ReadFatChain(uint startSector, long size)
    {
        const long MaxStreamSize = 256L * 1024 * 1024;
        var parts = new List<byte[]>();
        long total = 0;
        var sector = startSector;
        var visited = 0;
        while (sector != EndOfChain && sector != FreeSect && ++visited < 1_000_000)
        {
            if (sector >= _fat.Length)
                throw new InvalidDataException("流扇区越界,文件已损坏。");
            var data = ReadSectorBytes(sector);
            parts.Add(data);
            total += data.Length;
            if (total > MaxStreamSize)
                throw new InvalidDataException("流长度异常,文件已损坏。");
            sector = _fat[sector];
        }
        if (total < size)
            throw new InvalidDataException("流数据不完整,文件已损坏。");

        var result = new byte[total];
        var offset = 0;
        foreach (var p in parts)
        {
            Array.Copy(p, 0, result, offset, p.Length);
            offset += p.Length;
        }
        return result;
    }

    private byte[] ReadSectorBytes(uint sector) => ReadRawSector(_dataOffset, sector);

    /// <summary>目录扇区校验:存在类型 5(根目录)且带名称的条目(数据偏移探测用)。</summary>
    private static bool HasRootEntry(byte[] dirSector)
    {
        for (var off = 0; off + 128 <= dirSector.Length; off += 128)
        {
            var nameLen = ReadU16(dirSector, off + 0x40);
            var type = dirSector[off + 0x42];
            if (type == 5 && nameLen >= 12) return true;
        }
        return false;
    }

    private byte[] ReadRawSector(long dataOffset, uint sector)
    {
        var offset = dataOffset + (long)sector * _sectorSize;
        if (offset + _sectorSize > _fs.Length)
            throw new InvalidDataException("扇区超出文件范围,文件已损坏。");
        _fs.Seek(offset, SeekOrigin.Begin);
        var buf = new byte[_sectorSize];
        _fs.ReadExactly(buf);
        return buf;
    }

    internal static ushort ReadU16(ReadOnlySpan<byte> data, int offset)
        => (ushort)(data[offset] | (data[offset + 1] << 8));

    internal static uint ReadU32(ReadOnlySpan<byte> data, int offset)
        => (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));

    private static ulong ReadU64(ReadOnlySpan<byte> data, int offset)
        => ReadU32(data, offset) | ((ulong)ReadU32(data, offset + 4) << 32);
}
