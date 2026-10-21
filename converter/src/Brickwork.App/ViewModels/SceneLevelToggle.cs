using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Brickwork.Core.Models;

namespace Brickwork.App.ViewModels;

/// <summary>
/// One "on level N" checkbox for a list row: checked when every entity of the row is on the level,
/// indeterminate when only some are.
/// </summary>
public sealed partial class SceneLevelToggle : ObservableObject
{
    private readonly EditorSession _session;
    private readonly Func<IEnumerable<HashSet<int>>> _levelSets;

    private SceneLevelToggle(EditorSession session, int level, Func<IEnumerable<HashSet<int>>> levelSets)
    {
        _session = session;
        Level = level;
        _levelSets = levelSets;
    }

    public int Level { get; }

    public string Label => Level.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public string ToolTip => $"On level {Level}";

    public bool? IsChecked
    {
        get
        {
            var count = _session.SceneLevelCount;
            var states = _levelSets().Select(levels => SceneLevels.IsOn(levels, Level, count)).Distinct().ToList();
            return states.Count switch
            {
                0 => false,
                1 => states[0],
                _ => null,
            };
        }
        set
        {
            // A click on an indeterminate box reaches here as null; treat it as "put all on".
            var on = value ?? true;
            var sets = _levelSets().ToList();
            _session.Execute(on ? $"Put on level {Level}" : $"Take off level {Level}", () =>
            {
                foreach (var levels in sets)
                {
                    SceneLevels.Set(levels, Level, on, _session.SceneLevelCount);
                }
            });
            OnPropertyChanged();
        }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsChecked));

    /// <summary>
    /// Checkboxes for every scene level, or none when there is only one level.
    /// <paramref name="levelSets"/> is read on every use, so it follows undo and new children.
    /// </summary>
    public static ObservableCollection<SceneLevelToggle> CreateFor(
        EditorSession session,
        Func<IEnumerable<HashSet<int>>> levelSets)
    {
        var count = Math.Clamp(session.SceneLevelCount, 1, SceneLevels.MaxCount);
        return count <= 1
            ? []
            : new ObservableCollection<SceneLevelToggle>(
                Enumerable.Range(1, count).Select(level => new SceneLevelToggle(session, level, levelSets)));
    }

    public static void RefreshAll(IEnumerable<SceneLevelToggle> toggles)
    {
        foreach (var toggle in toggles)
        {
            toggle.Refresh();
        }
    }
}
