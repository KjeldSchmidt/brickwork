using System.IO.Compression;
using System.Text.Json;

namespace InkarnateTools.App;

internal static class SourceJsonReader
{
    public static async Task<string> ReadPrettyAsync(string path)
    {
        await using var file = File.OpenRead(path);
        GZipStream? gzip = null;
        Stream readable = file;
        if (IsGZip(file))
        {
            gzip = new GZipStream(file, CompressionMode.Decompress, leaveOpen: true);
            readable = gzip;
        }

        try
        {
            using var reader = new StreamReader(readable, leaveOpen: true);
            var raw = await reader.ReadToEndAsync().ConfigureAwait(false);
            using var document = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(
                document.RootElement,
                new JsonSerializerOptions { WriteIndented = true });
        }
        finally
        {
            if (gzip is not null)
            {
                await gzip.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static bool IsGZip(Stream stream)
    {
        Span<byte> header = stackalloc byte[2];
        var read = stream.Read(header);
        stream.Position = 0;
        return read == 2 && header[0] == 0x1f && header[1] == 0x8b;
    }
}
