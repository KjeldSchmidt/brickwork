using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Mvvm.Controls;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

/// <summary>Lists the map's lights grouped by kind (e.g. "Hand Lantern Lit"), with per-kind settings.</summary>
public partial class LightsToolViewModel : Tool
{
    private readonly EditorSession _session;
    private bool _syncingSelection;

    [ObservableProperty]
    private ObservableCollection<LightKindViewModel> _kinds = [];

    [ObservableProperty]
    private object? _selectedTreeItem;

    public LightsToolViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                Rebuild();
            }

            if (args.PropertyName is nameof(EditorSession.ContentRevision) && !_session.IsGestureActive)
            {
                Sync();
            }

            if (args.PropertyName is nameof(EditorSession.ShowLightRadii))
            {
                OnPropertyChanged(nameof(ShowLightRadii));
            }

            if (args.PropertyName is nameof(EditorSession.FocusedLightEntityId) &&
                _session.FocusedLightEntityId is null &&
                SelectedTreeItem is LightItemViewModel)
            {
                _syncingSelection = true;
                SelectedTreeItem = null;
                _syncingSelection = false;
            }
        };
        Rebuild();
    }

    public bool HasLights => Kinds.Count > 0;

    public bool ShowEmptyMessage => !HasLights;

    public string EmptyMessage => _session.Map is null
        ? "Open a Source Map to see lights"
        : "No lights found. Stamps whose names contain a light word (lantern, torch, candle, …) become lights; the words can be changed under Settings → Import.";

    public string Summary
    {
        get
        {
            var total = Kinds.Sum(kind => kind.Lights.Count);
            var active = Kinds.Sum(kind => kind.Lights.Count(light => light.IsActive));
            return $"{active} of {total} lights exported. Radii are in grid squares.";
        }
    }

    public bool ShowLightRadii
    {
        get => _session.ShowLightRadii;
        set => _session.ShowLightRadii = value;
    }

    public void SetHoveredItem(object? item) =>
        _session.HoveredLightEntityId = item is LightItemViewModel light ? light.Light.EntityId : null;

    partial void OnSelectedTreeItemChanged(object? value)
    {
        if (_syncingSelection)
        {
            return;
        }

        _session.FocusedLightEntityId = value is LightItemViewModel light ? light.Light.EntityId : null;
    }

    private void Rebuild()
    {
        _syncingSelection = true;
        SelectedTreeItem = null;
        _syncingSelection = false;
        _session.FocusedLightEntityId = null;
        _session.HoveredLightEntityId = null;

        var expanded = Kinds.Where(kind => kind.IsExpanded).Select(kind => kind.Name).ToHashSet();
        var kinds = new ObservableCollection<LightKindViewModel>();
        if (_session.Map is { } map)
        {
            foreach (var group in map.Lights
                         .GroupBy(light => light.KindName, StringComparer.OrdinalIgnoreCase)
                         .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
            {
                kinds.Add(new LightKindViewModel(this, _session, group.Key, group.ToList())
                {
                    IsExpanded = expanded.Contains(group.Key),
                });
            }
        }

        Kinds = kinds;
        OnPropertyChanged(nameof(HasLights));
        OnPropertyChanged(nameof(ShowEmptyMessage));
        OnPropertyChanged(nameof(EmptyMessage));
        OnPropertyChanged(nameof(Summary));
    }

    /// <summary>Refreshes values in place, or rebuilds when lights were added or removed (e.g. by undo).</summary>
    private void Sync()
    {
        var map = _session.Map;
        var shown = Kinds.SelectMany(kind => kind.Lights).ToList();
        if (map is null ||
            shown.Count != map.Lights.Count ||
            !shown.Select(light => light.EntityId).ToHashSet().SetEquals(map.Lights.Select(light => light.EntityId)) ||
            shown.Any(light => !map.Lights.Contains(light)))
        {
            Rebuild();
            return;
        }

        foreach (var kind in Kinds)
        {
            kind.RefreshFromModel();
        }

        OnPropertyChanged(nameof(Summary));
    }

    internal void NotifySummaryChanged() => OnPropertyChanged(nameof(Summary));
}

public partial class LightKindViewModel : ObservableObject
{
    private readonly LightsToolViewModel _tool;
    private readonly EditorSession _session;

    [ObservableProperty]
    private bool _isExpanded;

    public LightKindViewModel(
        LightsToolViewModel tool,
        EditorSession session,
        string name,
        IReadOnlyList<LightSource> lights)
    {
        _tool = tool;
        _session = session;
        Name = name;
        Lights = lights;
        Children = new ObservableCollection<LightItemViewModel>(
            lights.Select((light, index) => new LightItemViewModel(tool, session, light, index + 1)));
    }

    public string Name { get; }

    public IReadOnlyList<LightSource> Lights { get; }

    public ObservableCollection<LightItemViewModel> Children { get; }

    public string Header => $"{Name} ({Lights.Count})";

    public bool? IsActive
    {
        get => Lights.All(light => light.IsActive) ? true : Lights.Any(light => light.IsActive) ? null : false;
        set
        {
            // A click on an indeterminate box reaches here as null; treat it as "enable all".
            var enabled = value ?? true;
            Apply(enabled ? "Enable lights" : "Disable lights", light => light.IsActive = enabled);
        }
    }

    public decimal? BrightRadius
    {
        get => CommonValue(light => light.BrightRadius);
        set
        {
            if (value is decimal radius)
            {
                Apply("Change bright radius", light => light.BrightRadius = (double)radius);
            }
        }
    }

    public decimal? DimRadius
    {
        get => CommonValue(light => light.DimRadius);
        set
        {
            if (value is decimal radius)
            {
                Apply("Change dim radius", light => light.DimRadius = (double)radius);
            }
        }
    }

    public string Color
    {
        get => Lights.Select(light => LightColors.Normalize(light.Color)).Distinct().Count() == 1
            ? LightColors.Normalize(Lights[0].Color)
            : string.Empty;
        set
        {
            if (LightColors.TryParse(value, out _, out _, out _))
            {
                var color = LightColors.Normalize(value);
                Apply("Change light color", light => light.Color = color);
            }
        }
    }

    public IReadOnlyList<LightAnimation> AnimationOptions => LightAnimations.All;

    /// <summary>Null when the lights of this kind use different animations.</summary>
    public LightAnimation? Animation
    {
        get => Lights.Select(light => light.Animation).Distinct().Count() == 1 ? Lights[0].Animation : null;
        set
        {
            if (value is LightAnimation animation && animation != Animation)
            {
                Apply("Change light animation", light => light.Animation = animation);
            }
        }
    }

    public bool HasAnimation => Animation is not (LightAnimation.None or null);

    public decimal? AnimationSpeed
    {
        get => CommonValue(light => light.AnimationSpeed);
        set
        {
            if (value is decimal speed)
            {
                Apply("Change animation speed", light => light.AnimationSpeed = (int)Math.Clamp(speed, 1, 10));
            }
        }
    }

    public decimal? AnimationIntensity
    {
        get => CommonValue(light => light.AnimationIntensity);
        set
        {
            if (value is decimal intensity)
            {
                Apply("Change animation intensity", light => light.AnimationIntensity = (int)Math.Clamp(intensity, 1, 10));
            }
        }
    }

    /// <summary>Picks the animation from the kind's name, as import does.</summary>
    [RelayCommand]
    private void DetectAnimation() => Animation = LightAnimations.Detect(Name);

    public IBrush ColorBrush =>
        LightColors.TryParse(Color, out var red, out var green, out var blue)
            ? new SolidColorBrush(Avalonia.Media.Color.FromRgb(red, green, blue))
            : Brushes.Transparent;

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(BrightRadius));
        OnPropertyChanged(nameof(DimRadius));
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(ColorBrush));
        OnPropertyChanged(nameof(Animation));
        OnPropertyChanged(nameof(HasAnimation));
        OnPropertyChanged(nameof(AnimationSpeed));
        OnPropertyChanged(nameof(AnimationIntensity));
        foreach (var child in Children)
        {
            child.RefreshFromModel();
        }
    }

    private decimal? CommonValue(Func<LightSource, double> selector)
    {
        var values = Lights.Select(selector).Distinct().ToList();
        return values.Count == 1 ? Math.Round((decimal)values[0], 2) : null;
    }

    private void Apply(string name, Action<LightSource> mutate)
    {
        _session.Execute(name, () =>
        {
            foreach (var light in Lights)
            {
                mutate(light);
            }
        });
        RefreshFromModel();
        _tool.NotifySummaryChanged();
    }
}

public partial class LightItemViewModel : ObservableObject
{
    private readonly LightsToolViewModel _tool;
    private readonly EditorSession _session;

    public LightItemViewModel(LightsToolViewModel tool, EditorSession session, LightSource light, int number)
    {
        _tool = tool;
        _session = session;
        Light = light;
        Number = number;
    }

    public LightSource Light { get; }

    public int Number { get; }

    public string DisplayName => string.Equals(Light.DisplayName, Light.KindName, StringComparison.OrdinalIgnoreCase)
        ? $"#{Number}"
        : $"#{Number} {Light.DisplayName}";

    public string PositionText => string.Create(
        CultureInfo.InvariantCulture,
        $"({Light.Position.X:0}, {Light.Position.Y:0})");

    public bool IsActive
    {
        get => Light.IsActive;
        set
        {
            if (Light.IsActive == value)
            {
                return;
            }

            _session.Execute(value ? "Enable light" : "Disable light", () => Light.IsActive = value);
            OnPropertyChanged();
            _tool.NotifySummaryChanged();
        }
    }

    public void RefreshFromModel()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(PositionText));
    }
}
