namespace Brickwork.Core.Models;

public sealed class TransactionAnalysis
{
    public required int TransactionId { get; init; }

    public required string CommandType { get; init; }

    public required TransactionUnderstanding Understanding { get; init; }

    public string? Detail { get; init; }

    public string? RawJson { get; init; }

    public IReadOnlyList<TransactionAnalysis> Children { get; init; } = [];
}
