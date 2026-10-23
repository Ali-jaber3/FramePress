using System.Buffers.Binary;
using System.Text;
namespace FramePress.Core;
public static class ImageSafety
{
    public static void EnsureStatic(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        using var stream = File.OpenRead(path);
        Span<byte> header = stackalloc byte[24];
        var read = stream.Read(header);
        if (extension == ".webp" && read >= 21 && Encoding.ASCII.GetString(header[..4]) == "RIFF" &&
            Encoding.ASCII.GetString(header[12..16]) == "VP8X" && (header[20] & 2) != 0)
            throw new IOException("Animated WebP skipped to preserve animation.");
        if (extension != ".png") return;
        stream.Position = 8;
        Span<byte> chunk = stackalloc byte[8];
        for (var count = 0; count < 1024 && stream.Position + 8 <= stream.Length; count++)
        {
            stream.ReadExactly(chunk); var length = BinaryPrimitives.ReadUInt32BigEndian(chunk[..4]);
            var type = Encoding.ASCII.GetString(chunk[4..]);
            if (type == "acTL") throw new IOException("Animated PNG skipped to preserve animation.");
            if (type is "IDAT" or "IEND") break;
            if (length + 4L > stream.Length - stream.Position) throw new IOException("Invalid PNG chunk.");
            stream.Seek(length + 4L, SeekOrigin.Current);
        }
    }
}
