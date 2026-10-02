using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace Foster.Framework;

/// <summary>Managed RGBA storage, QOI and non-interlaced 8-bit PNG; no SDL surface.</summary>
internal unsafe struct ImageData
{
    public enum Formats { PNG, QOI }
    private GCHandle handle;
    public readonly int Width, Height;
    public readonly int SizeInBytes => checked(Width * Height * 4);
    public readonly nint Data => handle.IsAllocated ? handle.AddrOfPinnedObject() : 0;
    public readonly Span<byte> Bytes => new((void*)Data, SizeInBytes);
    public readonly Span<Color> Pixels => new((void*)Data, Width * Height);
    private ImageData(Array pixels, int width, int height)
    {
        Width = width; Height = height;
        handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
    }
    public static ImageData Rgba<T>(T[] rgba, int width, int height) where T : unmanaged
    {
        if (width <= 0 || height <= 0 || rgba.LongLength * sizeof(T) != (long)width * height * 4)
            throw new ArgumentException("Invalid RGBA image dimensions or buffer length.");
        return new(rgba, width, height);
    }
    public static ImageData Decode(Stream stream)
    {
        using var memory = new MemoryStream(); stream.CopyTo(memory); return Decode(memory.ToArray());
    }
    public static ImageData Decode(ReadOnlySpan<byte> bytes)
    {
        if (Qoi.IsFormat(bytes))
        {
            var pixels = Qoi.Decode(bytes, 4, out var description);
            return Rgba(pixels, checked((int)description.Width), checked((int)description.Height));
        }
        ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
        if (!bytes.StartsWith(signature)) throw new InvalidDataException("Expected a PNG or QOI image.");
        int width = 0, height = 0, channels = 0, type = -1;
        byte[] palette = [], transparency = [];
        using var compressed = new MemoryStream();
        int cursor = 8;
        bool header = false, ended = false;
        while (cursor + 12 <= bytes.Length)
        {
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(bytes[cursor..]));
            if (length > bytes.Length - cursor - 12) throw new InvalidDataException("Truncated PNG chunk.");
            var name = bytes.Slice(cursor + 4, 4);
            var payload = bytes.Slice(cursor + 8, length);
            uint crc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(cursor + 8 + length, 4));
            if (Crc(bytes.Slice(cursor + 4, length + 4)) != crc) throw new InvalidDataException("PNG CRC mismatch.");
            if (name.SequenceEqual("IHDR"u8))
            {
                if (header || length != 13) throw new InvalidDataException("Invalid PNG header.");
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(payload[4..]));
                type = payload[9];
                if (payload[8] != 8 || payload[10] != 0 || payload[11] != 0 || payload[12] != 0)
                    throw new NotSupportedException("Foster.Web supports non-interlaced 8-bit PNG; convert this asset to RGBA8 or QOI.");
                channels = type switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new InvalidDataException("Invalid PNG color type.") };
                if (width <= 0 || height <= 0) throw new InvalidDataException("Invalid PNG size.");
                header = true;
            }
            else if (name.SequenceEqual("PLTE"u8)) palette = payload.ToArray();
            else if (name.SequenceEqual("tRNS"u8)) transparency = payload.ToArray();
            else if (name.SequenceEqual("IDAT"u8)) { if (!header) throw new InvalidDataException("Missing PNG header."); compressed.Write(payload); }
            else if (name.SequenceEqual("IEND"u8)) { ended = true; break; }
            cursor = checked(cursor + length + 12);
        }
        if (!header || !ended) throw new InvalidDataException("Incomplete PNG image.");
        int stride = checked(width * channels);
        var raw = new byte[checked((stride + 1) * height)];
        compressed.Position = 0;
        using (var zlib = new ZLibStream(compressed, CompressionMode.Decompress, true))
        {
            zlib.ReadExactly(raw);
            if (zlib.ReadByte() != -1) throw new InvalidDataException("Invalid PNG decompressed length.");
        }
        var rgba = new byte[checked(width * height * 4)];
        var previous = new byte[stride];
        var row = new byte[stride];
        for (int y = 0; y < height; y++)
        {
            int start = y * (stride + 1);
            for (int x = 0; x < stride; x++)
            {
                int left = x < channels ? 0 : row[x - channels];
                int up = previous[x], corner = x < channels ? 0 : previous[x - channels];
                int predictor = raw[start] switch { 0 => 0, 1 => left, 2 => up, 3 => (left + up) / 2, 4 => Paeth(left, up, corner), _ => throw new InvalidDataException("Invalid PNG filter.") };
                row[x] = unchecked((byte)(raw[start + x + 1] + predictor));
            }
            for (int x = 0; x < width; x++)
            {
                int s = x * channels, d = (y * width + x) * 4;
                byte r, g, b, a = 255;
                switch (type)
                {
                    case 0: r = g = b = row[s]; if (transparency.Length >= 2 && BinaryPrimitives.ReadUInt16BigEndian(transparency) == r) a = 0; break;
                    case 2:
                        r = row[s]; g = row[s + 1]; b = row[s + 2];
                        if (transparency.Length >= 6 && BinaryPrimitives.ReadUInt16BigEndian(transparency) == r && BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(2)) == g && BinaryPrimitives.ReadUInt16BigEndian(transparency.AsSpan(4)) == b) a = 0;
                        break;
                    case 3:
                        int index = row[s];
                        if (index * 3 + 2 >= palette.Length) throw new InvalidDataException("Invalid PNG palette index.");
                        r = palette[index * 3]; g = palette[index * 3 + 1]; b = palette[index * 3 + 2];
                        if (index < transparency.Length) a = transparency[index]; break;
                    case 4: r = g = b = row[s]; a = row[s + 1]; break;
                    default: r = row[s]; g = row[s + 1]; b = row[s + 2]; a = row[s + 3]; break;
                }
                rgba[d] = r; rgba[d + 1] = g; rgba[d + 2] = b; rgba[d + 3] = a;
            }
            (previous, row) = (row, previous);
        }
        return Rgba(rgba, width, height);
    }
    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, da = Math.Abs(p - a), db = Math.Abs(p - b), dc = Math.Abs(p - c);
        return da <= db && da <= dc ? a : db <= dc ? b : c;
    }
    public void Encode(Stream stream, Formats format)
    {
        if (Data == 0) throw new InvalidOperationException("Image is empty or disposed.");
        if (format == Formats.QOI)
        {
            stream.Write(Qoi.Encode(Data, new() { Width = (uint)Width, Height = (uint)Height, Channels = 4, Colorspace = 0 }));
            return;
        }
        stream.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Span<byte> header = stackalloc byte[13]; header.Clear();
        BinaryPrimitives.WriteUInt32BigEndian(header, (uint)Width);
        BinaryPrimitives.WriteUInt32BigEndian(header[4..], (uint)Height);
        header[8] = 8; header[9] = 6;
        Chunk(stream, "IHDR"u8, header);
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            for (int y = 0; y < Height; y++) { zlib.WriteByte(0); zlib.Write(Bytes.Slice(y * Width * 4, Width * 4)); }
        Chunk(stream, "IDAT"u8, compressed.ToArray());
        Chunk(stream, "IEND"u8, []);
    }
    private static void Chunk(Stream stream, ReadOnlySpan<byte> type, ReadOnlySpan<byte> payload)
    {
        Span<byte> word = stackalloc byte[4]; BinaryPrimitives.WriteUInt32BigEndian(word, (uint)payload.Length); stream.Write(word);
        var data = new byte[payload.Length + 4]; type.CopyTo(data); payload.CopyTo(data.AsSpan(4)); stream.Write(data);
        BinaryPrimitives.WriteUInt32BigEndian(word, Crc(data)); stream.Write(word);
    }
    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (byte b in bytes) { crc ^= b; for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0); }
        return ~crc;
    }
    public void Free() { if (handle.IsAllocated) handle.Free(); }
}
