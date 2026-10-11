using Avalonia.Controls;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Views.Dialogs;

public partial class SettingsWindow : Window
{
    private static SettingsWindow? _openInstance;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    public static void Show(Window owner, EditorSession session, SettingsCategory category)
    {
        if (_openInstance is { } existing)
        {
            if (existing.DataContext is SettingsWindowViewModel existingVm)
            {
                existingVm.SelectCategory(category);
            }

            existing.Activate();
            return;
        }

        var window = new SettingsWindow
        {
            DataContext = new SettingsWindowViewModel(session),
        };
        ((SettingsWindowViewModel)window.DataContext).SelectCategory(category);
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_openInstance, window))
            {
                _openInstance = null;
            }
        };
        _openInstance = window;
        window.Show(owner);
    }
}
