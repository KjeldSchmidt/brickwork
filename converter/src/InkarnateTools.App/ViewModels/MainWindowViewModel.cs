using CommunityToolkit.Mvvm.ComponentModel;
using Dock.Model.Controls;
using Dock.Model.Core;
using InkarnateTools.App.Dock;

namespace InkarnateTools.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly AppDockFactory _dockFactory;

    [ObservableProperty]
    private IRootDock? _layout;

    public EditorSession Session { get; }

    public MainWindowViewModel()
    {
        Session = new EditorSession();
        _dockFactory = new AppDockFactory(Session);

        var layout = _dockFactory.CreateLayout();
        _dockFactory.InitLayout(layout);
        Layout = layout;
    }
}
