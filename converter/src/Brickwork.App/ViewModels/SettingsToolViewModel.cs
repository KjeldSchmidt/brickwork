using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Mvvm.Controls;

namespace Brickwork.App.ViewModels;

public partial class SettingsToolViewModel : Tool
{
    private readonly EditorSession _session;

    public SettingsToolViewModel(EditorSession session)
    {
        _session = session;
        _session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(EditorSession.WallSimplificationTolerance))
            {
                OnPropertyChanged(nameof(WallSimplificationTolerance));
            }
        };
    }

    public string AppVersionLabel => $"Brickwork {GitHubIssueReporter.GetAppVersion()}";

    public double WallSimplificationTolerance
    {
        get => _session.WallSimplificationTolerance;
        set => _session.WallSimplificationTolerance = value;
    }
}
