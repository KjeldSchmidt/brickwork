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

    public IReadOnlyList<UnknownActionGroup> UnknownActionGroups =>
        EnumerateSelfAndDescendants(Transactions)
            .Where(transaction => transaction.Understanding == TransactionUnderstanding.Unknown)
            .GroupBy(DescribeUnknownAction)
            .Select(group => new UnknownActionGroup(group.Key, group.Count()))
            .OrderBy(group => group.Description)
            .ToList();

    public string FormatSummary()
    {
        var title = string.IsNullOrWhiteSpace(MapTitle) ? "Untitled Map" : MapTitle;
        var version = SourceVersion?.ToString() ?? "?";

        return string.Join(
            Environment.NewLine,
            $"{title} (v{version})",
            $"Transactions: {TotalTransactions}",
            $"  Fully understood: {FullyUnderstoodCount} ({FullyUnderstoodPercent:F0}%)",
            $"  Known, ignored:   {KnownIgnoredCount} ({KnownIgnoredPercent:F0}%)",
            $"  Unknown:          {UnknownCount} ({UnknownPercent:F0}%)");
    }

    public string FormatUnknownActions()
    {
        if (UnknownActionGroups.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            UnknownActionGroups.Select(group => $"  {group.Description} (×{group.Count})"));
    }

    public string FormatTransactions()
    {
        var transactionLines = FormatTransactionLines();
        if (string.IsNullOrEmpty(transactionLines))
        {
            return "Transactions:";
        }

        return $"Transactions:{Environment.NewLine}{transactionLines}";
    }

    public string FormatTransactionLines()
    {
        var lines = new List<string>();
        foreach (var transaction in Transactions)
        {
            AppendTransactionLines(lines, transaction, indent: 0);
        }

        return string.Join(Environment.NewLine, lines);
    }

    public string FormatDetails()
    {
        var lines = new List<string>();

        var unknownActions = FormatUnknownActions();
        if (!string.IsNullOrEmpty(unknownActions))
        {
            lines.Add("Unknown actions:");
            lines.Add(unknownActions);
            lines.Add(string.Empty);
        }

        lines.Add(FormatTransactions());

        return string.Join(Environment.NewLine, lines);
    }

    public string FormatDetailed() =>
        string.Join(Environment.NewLine, FormatSummary(), string.Empty, FormatDetails());

    private static void AppendTransactionLines(
        List<string> lines,
        TransactionAnalysis transaction,
        int indent)
    {
        var pad = new string(' ', 2 + indent * 2);
        var label = FormatUnderstanding(transaction.Understanding);
        var detail = string.IsNullOrWhiteSpace(transaction.Detail)
            ? string.Empty
            : $" — {transaction.Detail}";
        var idPrefix = transaction.TransactionId >= 0
            ? $"#{transaction.TransactionId} "
            : string.Empty;
        lines.Add($"{pad}{idPrefix}{transaction.CommandType} [{label}]{detail}");

        foreach (var child in transaction.Children)
        {
            AppendTransactionLines(lines, child, indent + 1);
        }
    }

    private static IEnumerable<TransactionAnalysis> EnumerateSelfAndDescendants(
        IEnumerable<TransactionAnalysis> transactions)
    {
        foreach (var transaction in transactions)
        {
            yield return transaction;
            foreach (var nested in EnumerateSelfAndDescendants(transaction.Children))
            {
                yield return nested;
            }
        }
    }

    private static string DescribeUnknownAction(TransactionAnalysis transaction)
    {
        if (!string.IsNullOrWhiteSpace(transaction.Detail))
        {
            return $"{transaction.CommandType}: {transaction.Detail}";
        }

        return transaction.CommandType;
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
