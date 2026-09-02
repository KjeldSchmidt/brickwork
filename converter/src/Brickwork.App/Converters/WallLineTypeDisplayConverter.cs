using System.Globalization;
using Avalonia.Data.Converters;
using Brickwork.Core.Models;

namespace Brickwork.App.Converters;

public sealed class WallLineTypeDisplayConverter : IValueConverter
{
    public static readonly WallLineTypeDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is WallLineType lineType ? lineType.ToDisplayName() : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
