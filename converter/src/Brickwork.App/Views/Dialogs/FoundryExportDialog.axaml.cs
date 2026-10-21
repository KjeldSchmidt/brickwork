using Avalonia.Controls;
using Avalonia.Interactivity;
using Brickwork.Core.Models;

namespace Brickwork.App.Views.Dialogs;

/// <summary>Asks for the Foundry scene grid (squares and pixels per square) before exporting.</summary>
public partial class FoundryExportDialog : Window
{
    private double _rowsPerColumn = 1;
    private bool _syncingRows;

    public FoundryExportDialog()
    {
        InitializeComponent();
    }

    public FoundryExportGrid? Result { get; private set; }

    /// <param name="mapGrid">The map's own grid, used for the aspect ratio and the hint.</param>
    /// <param name="initial">Values to start with (the last export, or the map grid with the default pixels).</param>
    public static async Task<FoundryExportGrid?> ShowAsync(Window owner, GridInfo mapGrid, FoundryExportGrid initial)
    {
        var dialog = new FoundryExportDialog
        {
            _rowsPerColumn = mapGrid.Columns > 0 ? mapGrid.Rows / (double)mapGrid.Columns : 1,
        };
        dialog.MapGridHint.Text = $"The map's grid is {mapGrid.Columns} × {mapGrid.Rows}. Changing the width keeps its proportions.";

        dialog._syncingRows = true;
        dialog.ColumnsBox.Value = initial.Columns;
        dialog.RowsBox.Value = initial.Rows;
        dialog.PixelsBox.Value = initial.PixelsPerSquare;
        dialog._syncingRows = false;
        dialog.UpdateSceneSize();

        await dialog.ShowDialog(owner);
        return dialog.Result;
    }

    private void OnColumnsChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (!_syncingRows && e.NewValue is decimal columns)
        {
            _syncingRows = true;
            RowsBox.Value = Math.Max(1, Math.Round(columns * (decimal)_rowsPerColumn));
            _syncingRows = false;
        }

        UpdateSceneSize();
    }

    private void OnSizeChanged(object? sender, NumericUpDownValueChangedEventArgs e) => UpdateSceneSize();

    private FoundryExportGrid? ReadGrid() =>
        ColumnsBox?.Value is decimal columns && RowsBox?.Value is decimal rows && PixelsBox?.Value is decimal pixels
            ? new FoundryExportGrid((int)columns, (int)rows, (int)pixels)
            : null;

    private void UpdateSceneSize()
    {
        if (SceneSizeText is null)
        {
            return;
        }

        SceneSizeText.Text = ReadGrid() is { IsValid: true } grid
            ? $"Scene: {grid.Columns * grid.PixelsPerSquare} × {grid.Rows * grid.PixelsPerSquare} px"
            : "Enter the grid size.";
    }

    private void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (ReadGrid() is not { IsValid: true } grid)
        {
            return;
        }

        Result = grid;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close();
}
