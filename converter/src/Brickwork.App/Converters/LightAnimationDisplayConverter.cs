using System.Globalization;
using Avalonia.Data.Converters;
using Brickwork.Core.Models;

namespace Brickwork.App.Converters;

public sealed class LightAnimationDisplayConverter : IValueConverter
{
    public static readonly LightAnimationDisplayConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is LightAnimation animation ? LightAnimations.DisplayName(animation) : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
