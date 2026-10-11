using Brickwork.Core.Models;

namespace Brickwork.Inkarnate.Parsing;

internal sealed class InkParseResult
{
    public required MapDocument Map { get; init; }

    public required IReadOnlyDictionary<int, string> PathWallTypes { get; init; }
}
