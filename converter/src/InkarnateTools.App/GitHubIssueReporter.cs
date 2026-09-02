using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using InkarnateTools.Core.Models;

namespace InkarnateTools.App;

public static class GitHubIssueReporter
{
    public const string RepositoryUrl = "https://github.com/KjeldSchmidt/inkarnate-uvtt2-converter";

    public static string BuildIssueUrl(string title, string body) =>
        $"{RepositoryUrl}/issues/new?title={Uri.EscapeDataString(title)}&body={Uri.EscapeDataString(body)}";

    public static string BuildCompatibilityIssueBody(
        MapDocument? map,
        CompatibilityReport? report,
        bool includeSourceFileNote,
        string? screenshotPath)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Summary");
        builder.AppendLine("Describe what you expected vs what happened.");
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

            var sample = FindFirstUnknownTransaction(report);
            if (sample is not null)
            {
                builder.AppendLine();
                builder.AppendLine("## Sample unknown transaction");
                builder.AppendLine($"Transaction {sample.TransactionId}: `{sample.CommandType}`");
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
            }
        }

        if (!string.IsNullOrWhiteSpace(screenshotPath))
        {
            builder.AppendLine();
            builder.AppendLine("## Screenshot");
            builder.AppendLine($"Saved locally: `{screenshotPath}`");
            builder.AppendLine("Please drag and drop the screenshot into this issue.");
        }

        if (includeSourceFileNote && map is not null)
        {
            builder.AppendLine();
            builder.AppendLine("## Source file");
            builder.AppendLine("I opted in to sharing the source `.ink` file. Please attach it manually to this issue.");
        }

        return builder.ToString().TrimEnd();
    }

    public static TransactionAnalysis? FindFirstUnknownTransaction(CompatibilityReport report)
    {
        foreach (var transaction in report.Transactions)
        {
            var match = FindFirstUnknownTransaction(transaction);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

    private static TransactionAnalysis? FindFirstUnknownTransaction(TransactionAnalysis transaction)
    {
        if (transaction.Understanding == TransactionUnderstanding.Unknown)
        {
            return transaction;
        }

        foreach (var child in transaction.Children)
        {
            var match = FindFirstUnknownTransaction(child);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }

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
