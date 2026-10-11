namespace Brickwork.App.Views.Panels;

internal static class ContextMenuHeaders
{
    public static string Format(string action, int count) =>
        count > 1 ? $"{action} All ({count})" : action;

    public static string FormatConvert(string destination, int count) =>
        count > 1 ? $"Convert All to {destination} ({count})" : $"Convert to {destination}";
}
