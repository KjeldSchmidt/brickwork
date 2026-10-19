using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dock.Model.Controls;
using Dock.Model.Core;
using Brickwork.App.Dock;
using Brickwork.App.Settings;
using Brickwork.App.Views.Dialogs;

namespace Brickwork.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppDockFactory _dockFactory;
    private readonly AppPreferences _preferences;

    [ObservableProperty]
    private IRootDock? _layout;

    public EditorSession Session { get; }

    public ImportToolViewModel? ImportTool => _dockFactory.ImportTool;

    public MainWindowViewModel()
    {
        Session = new EditorSession();
        _preferences = new AppPreferences(Session);
        _dockFactory = new AppDockFactory(Session);

        var layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(layout);
        Layout = layout;

        Session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.Map))
            {
                ExportCommand.NotifyCanExecuteChanged();
            }

            if (args.PropertyName is nameof(EditorSession.CanUndo) or nameof(EditorSession.UndoActionName))
            {
                OnPropertyChanged(nameof(UndoHeader));
                UndoCommand.NotifyCanExecuteChanged();
            }

            if (args.PropertyName is nameof(EditorSession.CanRedo) or nameof(EditorSession.RedoActionName))
            {
                OnPropertyChanged(nameof(RedoHeader));
                RedoCommand.NotifyCanExecuteChanged();
            }
        };
    }

    [RelayCommand]
    private async Task OpenSourceMapAsync()
    {
        if (ImportTool?.OpenSourceMapCommand is { } command)
        {
            await command.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    public string UndoHeader => Session.UndoActionName is { } name ? $"_Undo {name}" : "_Undo";

    public string RedoHeader => Session.RedoActionName is { } name ? $"_Redo {name}" : "_Redo";

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Session.Undo();

    private bool CanUndo() => Session.CanUndo;

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Session.Redo();

    private bool CanRedo() => Session.CanRedo;

    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync()
    {
        if (ImportTool?.ConvertCommand is { } command)
        {
            await command.ExecuteAsync(null).ConfigureAwait(true);
        }
    }

    private bool CanExport() => Session.Map is not null;

    [RelayCommand]
    private void OpenImportSettings() => ShowSettings(SettingsCategory.Import);

    [RelayCommand]
    private void OpenExportSettings() => ShowSettings(SettingsCategory.Export);

    [RelayCommand]
    private void OpenSettings() => ShowSettings(SettingsCategory.Import);

    private void ShowSettings(SettingsCategory category)
    {
        if (GetMainWindow() is not { } owner)
        {
            return;
        }

        SettingsWindow.Show(owner, Session, category);
    }

    [RelayCommand]
    private void Exit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    private async Task AboutAsync()
    {
        if (GetMainWindow() is not { } owner)
        {
            return;
        }

        await AboutWindow.ShowAsync(owner).ConfigureAwait(true);
    }

    private static Avalonia.Controls.Window? GetMainWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        return desktop.MainWindow;
    }
}
