using System.Text.Json;
using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate.Parsing;

namespace InkarnateTools.Inkarnate;

public static class InkarnateFileParser
{
    public static async Task<MapDocument> ParseAsync(Stream source, CancellationToken cancellationToken = default)
    {
        await using var jsonStream = await InkJsonStream.OpenAsync(source, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return InkarnateDocumentParser.Parse(document.RootElement);
    }
}
