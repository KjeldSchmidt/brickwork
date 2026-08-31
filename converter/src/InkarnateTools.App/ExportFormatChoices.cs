using Avalonia.Platform.Storage;

namespace InkarnateTools.App;

internal sealed record ExportFormatChoice(string FormatId, string Label, string Extension)
{
    public FilePickerFileType ToFileType() =>
        new(Label) { Patterns = [$"*{Extension}"] };

    public static IReadOnlyList<ExportFormatChoice> ForFormats(IEnumerable<string> formatIds) =>
        formatIds
            .Select(formatId => formatId.ToLowerInvariant() switch
            {
                "uvtt1" => new ExportFormatChoice(formatId, "Universal VTT (UVTT1)", ".uvtt"),
                "uvtt2" => new ExportFormatChoice(formatId, "Universal VTT 2 (UVTT2)", ".uvtt2"),
                "foundry" => new ExportFormatChoice(formatId, "Foundry VTT", ".json"),
                _ => new ExportFormatChoice(formatId, formatId, ".json"),
            })
            .ToList();
}
