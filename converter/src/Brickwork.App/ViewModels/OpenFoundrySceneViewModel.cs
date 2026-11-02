using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Brickwork.Composition;
using Brickwork.Core.Foundry;
using Brickwork.Exporters.Foundry;

namespace Brickwork.App.ViewModels;

public sealed record FoundryWorldListItem(string Id, string Title)
{
    public string Display => string.IsNullOrWhiteSpace(Title) ? Id : $"{Title} ({Id})";
}

public sealed record FoundrySceneListItem(
    string Id,
    string Name,
    int WallCount,
    int RegionCount,
    string FolderPath = "")
{
    public string Display
    {
        get
        {
            var label = string.IsNullOrWhiteSpace(FolderPath)
                ? Name
                : $"{FolderPath} / {Name}";
            return $"{label}  ·  {WallCount} walls, {RegionCount} regions";
        }
    }
}

public partial class OpenFoundrySceneViewModel : ObservableObject
{
    private readonly FoundryWorldCatalog _catalog = ServiceFactory.CreateFoundryWorldCatalog();
    private readonly string? _mapsPath;

    [ObservableProperty]
    private string _dataRootStatus = string.Empty;

    [ObservableProperty]
    private string? _dataRoot;

    [ObservableProperty]
    private ObservableCollection<FoundryWorldListItem> _worlds = [];

    [ObservableProperty]
    private FoundryWorldListItem? _selectedWorld;

    [ObservableProperty]
    private ObservableCollection<FoundrySceneListItem> _scenes = [];

    [ObservableProperty]
    private FoundrySceneListItem? _selectedScene;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _canOpen;

    public string? SelectedWorldId => SelectedWorld?.Id;

    public string? SelectedSceneId => SelectedScene?.Id;

    public bool Confirmed { get; private set; }

    public OpenFoundrySceneViewModel(string? mapsPath)
    {
        _mapsPath = mapsPath;
        ReloadWorlds();
    }

    partial void OnSelectedWorldChanged(FoundryWorldListItem? value) => ReloadScenes();

    partial void OnSelectedSceneChanged(FoundrySceneListItem? value) => UpdateCanOpen();

    [RelayCommand]
    private void Refresh() => ReloadWorlds();

    [RelayCommand(CanExecute = nameof(CanOpen))]
    private void Open()
    {
        if (SelectedWorld is null || SelectedScene is null || string.IsNullOrWhiteSpace(DataRoot))
        {
            return;
        }

        Confirmed = true;
    }

    private void ReloadWorlds()
    {
        StatusMessage = string.Empty;
        Worlds.Clear();
        Scenes.Clear();
        SelectedWorld = null;
        SelectedScene = null;

        DataRoot = FoundryPaths.TryResolveDataRootFromMapsPath(_mapsPath);
        if (string.IsNullOrWhiteSpace(DataRoot) || !Directory.Exists(DataRoot))
        {
            DataRootStatus = "No Foundry Data root found. Set the maps folder in Export settings.";
            UpdateCanOpen();
            return;
        }

        DataRootStatus = $"Data root: {DataRoot}";

        try
        {
            var worlds = _catalog.ListWorlds(DataRoot);
            foreach (var world in worlds)
            {
                Worlds.Add(new FoundryWorldListItem(world.Id, world.Title));
            }

            if (Worlds.Count == 0)
            {
                StatusMessage = "No worlds found under Data/worlds.";
            }
            else
            {
                SelectedWorld = Worlds[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to list worlds: {ex.Message}";
        }

        UpdateCanOpen();
    }

    private void ReloadScenes()
    {
        Scenes.Clear();
        SelectedScene = null;

        if (SelectedWorld is null || string.IsNullOrWhiteSpace(DataRoot))
        {
            UpdateCanOpen();
            return;
        }

        try
        {
            var scenes = _catalog.ListScenes(DataRoot, SelectedWorld.Id);
            foreach (var scene in scenes)
            {
                Scenes.Add(new FoundrySceneListItem(
                    scene.Id,
                    scene.Name,
                    scene.WallCount,
                    scene.RegionCount,
                    scene.FolderPath));
            }

            StatusMessage = scenes.Count == 0
                ? "No scenes in this world (or scenes DB missing)."
                : $"{scenes.Count} scene(s).";

            if (Scenes.Count > 0)
            {
                SelectedScene = Scenes[0];
            }
        }
        catch (FoundryWorldDataLockedException ex)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to list scenes: {ex.Message}";
        }

        UpdateCanOpen();
    }

    private void UpdateCanOpen()
    {
        CanOpen = SelectedWorld is not null &&
                  SelectedScene is not null &&
                  !string.IsNullOrWhiteSpace(DataRoot);
        OpenCommand.NotifyCanExecuteChanged();
    }
}
