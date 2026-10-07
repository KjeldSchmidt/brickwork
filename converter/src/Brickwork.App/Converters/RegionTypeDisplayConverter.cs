using System.Globalization;
using Avalonia.Data.Converters;
using Brickwork.Core.Models;

namespace Brickwork.App.Converters;

public sealed class RegionTypeDisplayConverter : IValueConverter
{
    public static readonly RegionTypeDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RegionType regionType ? regionType.ToDisplayName() : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
