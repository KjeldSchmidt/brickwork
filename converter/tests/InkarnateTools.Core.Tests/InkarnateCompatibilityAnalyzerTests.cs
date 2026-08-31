using InkarnateTools.Core.Models;
using InkarnateTools.Inkarnate;
using InkarnateTools.Inkarnate.Parsing;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class InkarnateCompatibilityAnalyzerTests
{
    private static string EmptyBackupInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "empty-backup.ink"));

    [Fact]
    public async Task AnalyzeAsync_ReportsEmptyBackupCompatibility()
    {
        Assert.True(File.Exists(EmptyBackupInkPath), $"Missing test resource: {EmptyBackupInkPath}");

        var analyzer = new InkarnateCompatibilityAnalyzer();
        await using var input = File.OpenRead(EmptyBackupInkPath);

        var report = await analyzer.AnalyzeAsync(input);

        Assert.Equal("empty", report.MapTitle);
        Assert.Equal(3, report.SourceVersion);
        Assert.Equal(6, report.TotalTransactions);
        Assert.Equal(1, report.FullyUnderstoodCount);
        Assert.Equal(5, report.KnownIgnoredCount);
        Assert.Equal(0, report.UnknownCount);
    }

    [Fact]
    public async Task AnalyzeAsync_IdentifiesUnknownCommandTypes()
    {
        const string json = """
            {
              "title": "unknown commands",
              "version": 3,
              "history": [
                { "transactionId": 1, "cmdType": "cmd-layer-add", "layerKind": "brush" },
                { "transactionId": 2, "cmdType": "cmd-mystery" }
              ]
            }
            """;

        var analyzer = new InkarnateCompatibilityAnalyzer();
        await using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        var report = await analyzer.AnalyzeAsync(input);

        Assert.Equal(2, report.TotalTransactions);
        Assert.Equal(0, report.FullyUnderstoodCount);
        Assert.Equal(1, report.KnownIgnoredCount);
        Assert.Equal(1, report.UnknownCount);
        Assert.Equal("cmd-mystery", report.Transactions[1].CommandType);
        Assert.Equal([new UnknownActionGroup("cmd-mystery", 1)], report.UnknownActionGroups);
        Assert.Contains("cmd-mystery", report.FormatDetailed(), StringComparison.Ordinal);
    }

    [Fact]
    public void ImportAndAnalyze_ShareCompatibilityFromSameParser()
    {
        const string json = """
            {
              "title": "shared parser",
              "version": 3,
              "history": [
                { "transactionId": 1, "cmdType": "cmd-layer-add", "layerKind": "brush" },
                {
                  "transactionId": 2,
                  "cmdType": "cmd-entity-add",
                  "items": [
                    {
                      "layerId": "layer-grid-70",
                      "entity": {
                        "entityType": "grid",
                        "style": { "size": 100 }
                      }
                    }
                  ]
                }
              ]
            }
            """;

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var map = InkarnateDocumentParser.Parse(document.RootElement);

        Assert.NotNull(map.Compatibility);
        Assert.Equal(2, map.Compatibility!.TotalTransactions);
        Assert.Equal(1, map.Compatibility.FullyUnderstoodCount);
        Assert.Equal(1, map.Compatibility.KnownIgnoredCount);
        Assert.Equal(100, map.Grid.CellSize);
    }
}
