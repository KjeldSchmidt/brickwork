using System.Text;
using InkarnateTools.Composition;
using InkarnateTools.Inkarnate;
using Xunit;

namespace InkarnateTools.Core.Tests;

public class ConvertMapServiceTests
{
    private static string EmptyBackupInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "empty-backup.ink"));

    [Fact]
    public void ServiceFactory_RegistersExpectedExportFormats()
    {
        var service = ServiceFactory.CreateConvertMapService();

        Assert.Equal(["foundry", "uvtt1", "uvtt2"], service.SupportedExportFormats);
    }

    [Fact]
    public async Task ConvertAsync_WritesPlaceholderExport_FromPlainJson()
    {
        var service = ServiceFactory.CreateConvertMapService();
        var inputJson = """{"title":"Test Map","version":3,"scene":{"normSceneSize":{"w":100,"h":100}}}""";
        await using var input = new MemoryStream(Encoding.UTF8.GetBytes(inputJson));
        await using var output = new MemoryStream();

        await service.ConvertAsync(input, output, "uvtt2");

        output.Position = 0;
        using var reader = new StreamReader(output, Encoding.UTF8, leaveOpen: true);
        var result = await reader.ReadToEndAsync();

        Assert.Contains("\"format\": \"uvtt2\"", result, StringComparison.Ordinal);
        Assert.Contains("Test Map", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_WritesPlaceholderExport_FromInkBackup()
    {
        Assert.True(File.Exists(EmptyBackupInkPath), $"Missing test resource: {EmptyBackupInkPath}");

        var service = ServiceFactory.CreateConvertMapService();
        await using var input = File.OpenRead(EmptyBackupInkPath);
        await using var output = new MemoryStream();

        await service.ConvertAsync(input, output, "uvtt2");

        output.Position = 0;
        using var reader = new StreamReader(output, Encoding.UTF8, leaveOpen: true);
        var result = await reader.ReadToEndAsync();

        Assert.Contains("\"format\": \"uvtt2\"", result, StringComparison.Ordinal);
        Assert.Contains("empty", result, StringComparison.Ordinal);
        Assert.Contains("\"Columns\": 40", result, StringComparison.Ordinal);
        Assert.Contains("\"Rows\": 30", result, StringComparison.Ordinal);
        Assert.Contains("\"PixelsPerCell\": 51", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConvertAsync_ThrowsForUnknownFormat()
    {
        var service = ServiceFactory.CreateConvertMapService();
        await using var input = new MemoryStream("{}"u8.ToArray());
        await using var output = new MemoryStream();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConvertAsync(input, output, "unknown"));

        Assert.Contains("Unknown export format", exception.Message, StringComparison.Ordinal);
    }
}

public class InkarnateImporterTests
{
    private static string EmptyBackupInkPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "resources", "empty-backup.ink"));

    [Fact]
    public async Task ImportAsync_ReadsEssentialMetadata_FromInkBackup()
    {
        Assert.True(File.Exists(EmptyBackupInkPath), $"Missing test resource: {EmptyBackupInkPath}");

        var importer = new InkarnateImporter();
        await using var input = File.OpenRead(EmptyBackupInkPath);

        var map = await importer.ImportAsync(input);

        Assert.Equal("empty", map.Name);
        Assert.Equal(3, map.SourceVersion);
        Assert.Equal(8192, map.Scene.Width);
        Assert.Equal(6144, map.Scene.Height);
        Assert.NotNull(map.Preview);
        Assert.Equal(2048, map.Preview!.Width);
        Assert.Equal(1536, map.Preview.Height);
        Assert.Equal(204.8, map.Grid.CellSize, precision: 1);
        Assert.Equal(40, map.Grid.Columns);
        Assert.Equal(30, map.Grid.Rows);
        Assert.Equal(51, map.Grid.PixelsPerCell);
        Assert.Empty(map.Walls);
        Assert.Empty(map.Lights);
    }
}
