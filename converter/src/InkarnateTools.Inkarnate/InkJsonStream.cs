using System.IO.Compression;

namespace InkarnateTools.Inkarnate;

internal static class InkJsonStream
{
    public static async Task<Stream> OpenAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var buffer = source;

        if (!source.CanSeek)
        {
            var memoryStream = new MemoryStream();
            await source.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
            memoryStream.Position = 0;
            buffer = memoryStream;
        }

        if (IsGZip(buffer))
        {
            buffer.Position = 0;
            return new GZipStream(buffer, CompressionMode.Decompress, leaveOpen: true);
        }

        buffer.Position = 0;
        return buffer;
    }

    private static bool IsGZip(Stream stream)
    {
        Span<byte> header = stackalloc byte[2];
        var read = stream.Read(header);
        stream.Position = 0;
        return read == 2 && header[0] == 0x1f && header[1] == 0x8b;
    }
}
