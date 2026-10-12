using System.Runtime.InteropServices;

namespace Brickwork.Core.Foundry;

public static class FoundryPaths
{
    /// <summary>Image extensions probed on disk, in preference order when several exist.</summary>
    private static readonly string[] BackgroundImageExtensions = [".webp", ".jpg", ".jpeg", ".png"];

    /// <summary>Default Foundry user-data root for the current OS, if it can be determined.</summary>
    public static string? TryGetDefaultUserDataDirectory()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return string.IsNullOrWhiteSpace(local) ? null : Path.Combine(local, "FoundryVTT");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            // ~/Library/Application Support
            var appSupport = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return string.IsNullOrWhiteSpace(appSupport) ? null : Path.Combine(appSupport, "FoundryVTT");
        }

        // Linux: first existing candidate, else the XDG default.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FoundryVTT"),
            string.IsNullOrWhiteSpace(home) ? null : Path.Combine(home, "FoundryVTT"),
            "/local/FoundryVTT",
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
    }

    /// <summary>Default Foundry <c>Data</c> directory (asset root), preferring an existing folder.</summary>
    public static string? TryGetDefaultDataDirectory()
    {
        var userData = TryGetDefaultUserDataDirectory();
        if (userData is null)
        {
            return null;
        }

        var data = Path.Combine(userData, "Data");
        if (Directory.Exists(data))
        {
            return data;
        }

        return Directory.Exists(userData) ? data : data;
    }

    /// <summary>
    /// Preferred maps folder for the settings default: <c>Data/maps</c> if present, otherwise <c>Data</c>.
    /// </summary>
    public static string? TryGetPreferredMapsDirectory()
    {
        var data = TryGetDefaultDataDirectory();
        if (data is null)
        {
            return null;
        }

        var maps = Path.Combine(data, "maps");
        if (Directory.Exists(maps))
        {
            return maps;
        }

        return Directory.Exists(data) ? data : maps;
    }

    /// <summary>
    /// Walks up from <paramref name="directory"/> until a folder named <c>Data</c> is found
    /// (Foundry's asset root).
    /// </summary>
    public static string? TryFindDataRoot(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        DirectoryInfo? current;
        try
        {
            current = new DirectoryInfo(Path.GetFullPath(directory));
        }
        catch
        {
            return null;
        }

        while (current is not null)
        {
            if (string.Equals(current.Name, "Data", StringComparison.OrdinalIgnoreCase))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return null;
    }

    /// <summary>
    /// Builds a Foundry <c>background.src</c> path under <paramref name="mapsDirectory"/>,
    /// expressed relative to the Foundry Data root when possible.
    /// Picks an existing image file by base name when the maps folder is readable; otherwise <c>.webp</c>.
    /// </summary>
    public static string BuildBackgroundSrc(string? mapsDirectory, string? sourceFileName)
    {
        var baseName = Path.GetFileNameWithoutExtension(
            string.IsNullOrWhiteSpace(sourceFileName) ? "map" : sourceFileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "map";
        }

        if (string.IsNullOrWhiteSpace(mapsDirectory))
        {
            return NormalizeFoundryPath(baseName + ".webp");
        }

        string absoluteMaps;
        try
        {
            absoluteMaps = Path.GetFullPath(mapsDirectory);
        }
        catch
        {
            return NormalizeFoundryPath(baseName + ".webp");
        }

        var fileName = ResolveBackgroundFileName(absoluteMaps, baseName);
        var imagePath = Path.Combine(absoluteMaps, fileName);
        var dataRoot = TryFindDataRoot(absoluteMaps);
        if (dataRoot is not null)
        {
            var relative = Path.GetRelativePath(dataRoot, imagePath);
            return NormalizeFoundryPath(relative);
        }

        // Outside a Data tree: use "<folder>/<file>" as a best-effort relative path.
        var folderName = new DirectoryInfo(absoluteMaps).Name;
        return string.IsNullOrWhiteSpace(folderName)
            ? NormalizeFoundryPath(fileName)
            : NormalizeFoundryPath(Path.Combine(folderName, fileName));
    }

    /// <summary>
    /// Returns the on-disk image file name for <paramref name="baseName"/> in
    /// <paramref name="mapsDirectory"/>, or <c>{baseName}.webp</c> if none exist / unreadable.
    /// </summary>
    public static string ResolveBackgroundFileName(string mapsDirectory, string baseName)
    {
        var existing = TryFindExistingBackgroundFileName(mapsDirectory, baseName);
        return existing ?? baseName + ".webp";
    }

    /// <summary>
    /// Absolute path of an existing Foundry background image for <paramref name="sourceFileName"/>
    /// under <paramref name="mapsDirectory"/>, or null when none is found.
    /// </summary>
    public static string? TryResolveBackgroundImagePath(string? mapsDirectory, string? sourceFileName)
    {
        if (string.IsNullOrWhiteSpace(mapsDirectory))
        {
            return null;
        }

        var baseName = Path.GetFileNameWithoutExtension(
            string.IsNullOrWhiteSpace(sourceFileName) ? "map" : sourceFileName);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "map";
        }

        string absoluteMaps;
        try
        {
            absoluteMaps = Path.GetFullPath(mapsDirectory);
        }
        catch
        {
            return null;
        }

        var fileName = TryFindExistingBackgroundFileName(absoluteMaps, baseName);
        if (fileName is null)
        {
            return null;
        }

        return Path.Combine(absoluteMaps, fileName);
    }

    private static string? TryFindExistingBackgroundFileName(string mapsDirectory, string baseName)
    {
        try
        {
            if (!Directory.Exists(mapsDirectory))
            {
                return null;
            }

            foreach (var extension in BackgroundImageExtensions)
            {
                var candidate = baseName + extension;
                if (File.Exists(Path.Combine(mapsDirectory, candidate)))
                {
                    return candidate;
                }
            }
        }
        catch
        {
            // Unreadable directory (permissions, IO).
        }

        return null;
    }

    private static string NormalizeFoundryPath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0)
        {
            return normalized;
        }

        // Foundry expects URL-escaped asset paths (spaces → %20, etc.), keeping '/' separators.
        return string.Join(
            '/',
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Select(Uri.EscapeDataString));
    }
}
