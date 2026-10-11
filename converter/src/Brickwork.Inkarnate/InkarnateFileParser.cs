using System.Text.Json;
using Brickwork.Core.Models;
using Brickwork.Inkarnate.Parsing;

namespace Brickwork.Inkarnate;

public static class InkarnateFileParser
{
    public static async Task<MapDocument> ParseAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var result = await ParseWithHintsAsync(source, cancellationToken).ConfigureAwait(false);
        return result.Map;
    }

    internal static async Task<InkParseResult> ParseWithHintsAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        await using var jsonStream = await InkJsonStream.OpenAsync(source, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(jsonStream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return InkarnateDocumentParser.ParseWithHints(document.RootElement);
    }
}
