using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using IconPacks.Avalonia.FontAwesome6;

namespace Brickwork.App.ViewModels;

public sealed partial class MapToolItemViewModel : ObservableObject
{
    public MapToolItemViewModel(
        MapToolKind kind,
        string name,
        string hotkey,
        PackIconFontAwesome6Kind iconKind,
        string? description = null,
        IReadOnlyList<MapToolButtonHint>? buttonHints = null,
        Thickness? iconMargin = null)
    {
        Kind = kind;
        Name = name;
        Hotkey = hotkey;
        IconKind = iconKind;
        Description = description;
        ButtonHints = buttonHints ?? [];
        IconMargin = iconMargin ?? default;
    }

    public MapToolKind Kind { get; }

    public string Name { get; }

    public string Hotkey { get; }

    public PackIconFontAwesome6Kind IconKind { get; }

    public Thickness IconMargin { get; }

    public string? Description { get; }

    public IReadOnlyList<MapToolButtonHint> ButtonHints { get; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasButtonHints => ButtonHints.Count > 0;

    [ObservableProperty]
    private bool _isSelected;
}
