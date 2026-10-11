using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Brickwork.App.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly EditorSession _session;

    [ObservableProperty]
    private SettingsCategory _selectedCategory = SettingsCategory.Import;

    public SettingsWindowViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.WallSimplificationTolerance))
            {
                OnPropertyChanged(nameof(WallSimplificationTolerance));
            }

            if (args.PropertyName is nameof(EditorSession.ShowDebugPanel))
            {
                OnPropertyChanged(nameof(ShowDebugPanel));
            }

            if (args.PropertyName is nameof(EditorSession.FoundryMapsPath))
            {
                OnPropertyChanged(nameof(FoundryMapsPath));
            }
        };
    }

    public bool IsImportSelected => SelectedCategory == SettingsCategory.Import;

    public bool IsExportSelected => SelectedCategory == SettingsCategory.Export;

    public bool IsGeneralSelected => SelectedCategory == SettingsCategory.General;

    public double WallSimplificationTolerance
    {
        get => _session.WallSimplificationTolerance;
        set => _session.WallSimplificationTolerance = value;
    }

    public bool ShowDebugPanel
    {
        get => _session.ShowDebugPanel;
        set => _session.ShowDebugPanel = value;
    }

    public string FoundryMapsPath
    {
        get => _session.FoundryMapsPath;
        set => _session.FoundryMapsPath = value ?? string.Empty;
    }

    public void SelectCategory(SettingsCategory category) => SelectedCategory = category;

    [RelayCommand]
    private void SelectImport() => SelectCategory(SettingsCategory.Import);

    [RelayCommand]
    private void SelectExport() => SelectCategory(SettingsCategory.Export);

    [RelayCommand]
    private void SelectGeneral() => SelectCategory(SettingsCategory.General);

    partial void OnSelectedCategoryChanged(SettingsCategory value)
    {
        OnPropertyChanged(nameof(IsImportSelected));
        OnPropertyChanged(nameof(IsExportSelected));
        OnPropertyChanged(nameof(IsGeneralSelected));
    }
}
