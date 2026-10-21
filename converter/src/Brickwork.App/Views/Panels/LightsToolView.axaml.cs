using Avalonia.Controls;
using Avalonia.Input;
using Brickwork.App.ViewModels;

namespace Brickwork.App.Views.Panels;

public partial class LightsToolView : UserControl
{
    public LightsToolView()
    {
        InitializeComponent();
    }

    private void OnLightPointerEntered(object? sender, PointerEventArgs e)
    {
        if (DataContext is LightsToolViewModel viewModel && sender is Control { DataContext: LightItemViewModel item })
        {
            viewModel.SetHoveredItem(item);
        }
    }

    private void OnTreePointerExited(object? sender, PointerEventArgs e)
    {
        if (DataContext is LightsToolViewModel viewModel)
        {
            viewModel.SetHoveredItem(null);
        }
    }
}
