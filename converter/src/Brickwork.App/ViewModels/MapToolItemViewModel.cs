using CommunityToolkit.Mvvm.ComponentModel;

namespace Brickwork.App.ViewModels;

public sealed partial class MapToolItemViewModel : ObservableObject
{
    public MapToolItemViewModel(
        MapToolKind kind,
        string name,
        string hotkey,
        string iconGeometry,
        string? description = null,
        IReadOnlyList<MapToolButtonHint>? buttonHints = null)
    {
        Kind = kind;
        Name = name;
        Hotkey = hotkey;
        IconGeometry = iconGeometry;
        Description = description;
        ButtonHints = buttonHints ?? [];
    }

    public MapToolKind Kind { get; }

    public string Name { get; }

    public string Hotkey { get; }

    public string IconGeometry { get; }

    public string? Description { get; }

    public IReadOnlyList<MapToolButtonHint> ButtonHints { get; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasButtonHints => ButtonHints.Count > 0;

    [ObservableProperty]
    private bool _isSelected;
}
