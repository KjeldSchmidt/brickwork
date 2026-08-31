using CommunityToolkit.Mvvm.ComponentModel;
using InkarnateTools.Core.Models;

namespace InkarnateTools.App;

public sealed partial class EditorSession : ObservableObject
{
    [ObservableProperty]
    private MapDocument? _map;

    [ObservableProperty]
    private string? _sourceFileName;
}
