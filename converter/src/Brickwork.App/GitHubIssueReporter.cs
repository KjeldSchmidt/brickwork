using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Brickwork.Core.Models;

namespace Brickwork.App;

public static class GitHubIssueReporter
{
    public const string RepositoryUrl = "https://github.com/KjeldSchmidt/inkarnate-uvtt2-converter";

    /// <summary>Practical cross-browser limit for a pre-filled issues/new URL.</summary>
    public const int MaxIssueUrlLength = 7000;

    public static string BuildIssueUrl(string title, string body) =>
        $"{RepositoryUrl}/issues/new?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";

    public static string BuildCompatibilityIssueBody(
        MapDocument? map,
        CompatibilityReport? report,
        string? screenshotPath,
        string issueTitle = "Compatibility issue")
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Summary");
        builder.AppendLine("Feel free to add details here.");
        builder.AppendLine();
        builder.AppendLine("## Environment");
        builder.AppendLine($"- App version: {GetAppVersion()}");
        builder.AppendLine($"- OS: {RuntimeInformation.OSDescription}");
        builder.AppendLine($"- Runtime: {RuntimeInformation.FrameworkDescription}");

        if (map is not null)
        {
            builder.AppendLine($"- Map: {map.SourceFileName ?? map.Name ?? "unknown"}");
        }

        builder.AppendLine();
        builder.AppendLine("## Compatibility");

        if (report is null)
        {
            builder.AppendLine("No compatibility report available.");
        }
        else
        {
            builder.AppendLine("```");
            builder.AppendLine(report.FormatSummary().TrimEnd());
            if (report.UnknownCount > 0)
            {
                builder.AppendLine();
                builder.AppendLine(report.FormatUnknownActions().TrimEnd());
            }

            builder.AppendLine("```");
        }

        if (!string.IsNullOrWhiteSpace(screenshotPath))
        {
            builder.AppendLine();
            builder.AppendLine("## Screenshot");
            builder.AppendLine($"Saved locally: `{screenshotPath}`");
            builder.AppendLine("Please drag and drop the screenshot into this issue.");
        }

        builder.AppendLine();
        builder.AppendLine("## Source file");
        builder.AppendLine("Optional: attach the source `.ink` file to this issue if you are willing to share it.");

        var fixedBody = builder.ToString().TrimEnd();
        if (report is null || report.UnknownCount == 0)
        {
            return fixedBody;
        }

        return AppendUnknownTransactionsWithinUrlBudget(fixedBody, report, issueTitle);
    }

    private static string AppendUnknownTransactionsWithinUrlBudget(
        string fixedBody,
        CompatibilityReport report,
        string issueTitle)
    {
        var unknowns = EnumerateUnknownTransactions(report).ToList();
        if (unknowns.Count == 0)
        {
            return fixedBody;
        }

        var builder = new StringBuilder(fixedBody);
        builder.AppendLine();
        builder.AppendLine();
        builder.AppendLine("## Unknown transactions");

        var included = 0;
        foreach (var unknown in unknowns)
        {
            var sample = FormatUnknownTransactionSample(unknown, included == 0);
            var candidate = builder + sample;
            if (BuildIssueUrl(issueTitle, candidate).Length > MaxIssueUrlLength)
            {
                break;
            }

            builder.Append(sample);
            included++;
        }

        var omitted = unknowns.Count - included;
        if (omitted > 0)
        {
            var withNote = builder + FormatOmissionNote(omitted);
            if (BuildIssueUrl(issueTitle, withNote).Length <= MaxIssueUrlLength)
            {
                builder.Append(FormatOmissionNote(omitted));
            }
            else if (included == 0)
            {
                var fallback = fixedBody + FormatOmissionNote(unknowns.Count);
                if (BuildIssueUrl(issueTitle, fallback).Length <= MaxIssueUrlLength)
                {
                    return fallback.TrimEnd();
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static string FormatUnknownTransactionSample(TransactionAnalysis sample, bool first)
    {
        var builder = new StringBuilder();
        if (!first)
        {
            builder.AppendLine();
        }

        builder.AppendLine($"### Transaction {sample.TransactionId}: `{sample.CommandType}`");
        if (!string.IsNullOrWhiteSpace(sample.Detail))
        {
            builder.AppendLine($"Detail: {sample.Detail}");
        }

        if (!string.IsNullOrWhiteSpace(sample.RawJson))
        {
            builder.AppendLine();
            builder.AppendLine("```json");
            builder.AppendLine(sample.RawJson);
            builder.AppendLine("```");
        }

        return builder.ToString();
    }

    private static string FormatOmissionNote(int omittedCount) =>
        omittedCount <= 0
            ? string.Empty
            : $"\n\n_{omittedCount} more unknown transaction(s) omitted due to URL length; please attach the `.ink` if possible._";

    public static IEnumerable<TransactionAnalysis> EnumerateUnknownTransactions(CompatibilityReport report)
    {
        foreach (var transaction in report.Transactions)
        {
            foreach (var match in EnumerateUnknownTransactions(transaction))
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<TransactionAnalysis> EnumerateUnknownTransactions(TransactionAnalysis transaction)
    {
        if (transaction.Understanding == TransactionUnderstanding.Unknown)
        {
            yield return transaction;
        }

        foreach (var child in transaction.Children)
        {
            foreach (var match in EnumerateUnknownTransactions(child))
            {
                yield return match;
            }
        }
    }

    public static TransactionAnalysis? FindFirstUnknownTransaction(CompatibilityReport report) =>
        EnumerateUnknownTransactions(report).FirstOrDefault();

    public static string GetAppVersion() =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
        ?? "unknown";

    public static void OpenIssueInBrowser(string url)
    {
        var psi = new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true };
        System.Diagnostics.Process.Start(psi);
    }
}
