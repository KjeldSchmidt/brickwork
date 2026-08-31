namespace InkarnateTools.Core.Models;

public sealed class CompatibilityReport
{
    public string? MapTitle { get; init; }

    public int? SourceVersion { get; init; }

    public required IReadOnlyList<TransactionAnalysis> Transactions { get; init; }

    public int TotalTransactions => Transactions.Count;

    public int FullyUnderstoodCount =>
        Transactions.Count(transaction => transaction.Understanding == TransactionUnderstanding.FullyUnderstood);

    public int KnownIgnoredCount =>
        Transactions.Count(transaction => transaction.Understanding == TransactionUnderstanding.KnownIgnored);

    public int UnknownCount =>
        Transactions.Count(transaction => transaction.Understanding == TransactionUnderstanding.Unknown);

    public double FullyUnderstoodPercent =>
        TotalTransactions == 0 ? 100d : FullyUnderstoodCount * 100d / TotalTransactions;

    public double KnownIgnoredPercent =>
        TotalTransactions == 0 ? 0d : KnownIgnoredCount * 100d / TotalTransactions;

    public double UnknownPercent =>
        TotalTransactions == 0 ? 0d : UnknownCount * 100d / TotalTransactions;

    public string FormatSummary()
    {
        var title = string.IsNullOrWhiteSpace(MapTitle) ? "Untitled Map" : MapTitle;
        var version = SourceVersion?.ToString() ?? "?";

        return $"""
            {title} (v{version})
            Transactions: {TotalTransactions}
              Fully understood: {FullyUnderstoodCount} ({FullyUnderstoodPercent:F0}%)
              Known, ignored:   {KnownIgnoredCount} ({KnownIgnoredPercent:F0}%)
              Unknown:          {UnknownCount} ({UnknownPercent:F0}%)
            """;
    }

    public string FormatDetailed()
    {
        var lines = new List<string> { FormatSummary().TrimEnd(), string.Empty, "Transactions:" };

        foreach (var transaction in Transactions)
        {
            var label = FormatUnderstanding(transaction.Understanding);
            var detail = string.IsNullOrWhiteSpace(transaction.Detail)
                ? string.Empty
                : $" — {transaction.Detail}";
            lines.Add($"  #{transaction.TransactionId} {transaction.CommandType} [{label}]{detail}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatUnderstanding(TransactionUnderstanding understanding) =>
        understanding switch
        {
            TransactionUnderstanding.FullyUnderstood => "understood",
            TransactionUnderstanding.KnownIgnored => "ignored",
            TransactionUnderstanding.Unknown => "unknown",
            _ => understanding.ToString(),
        };
}
