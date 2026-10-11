using System.Text.Json;
using Brickwork.Core.Geometry;

namespace Brickwork.Core.Settings;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly string _settingsFilePath;

    public AppSettingsStore(string? directory = null)
    {
        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Brickwork");
        _settingsFilePath = Path.Combine(root, "settings.json");
    }

    public string SettingsFilePath => _settingsFilePath;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return CreateDefaults();
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            return settings is null ? CreateDefaults() : Normalize(settings);
        }
        catch
        {
            return CreateDefaults();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalized = Normalize(settings);
        var directory = Path.GetDirectoryName(_settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        var tempPath = _settingsFilePath + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _settingsFilePath, overwrite: true);
    }

    private static AppSettings CreateDefaults() => Normalize(new AppSettings());

    private static AppSettings Normalize(AppSettings settings)
    {
        if (double.IsNaN(settings.WallSimplificationTolerance) ||
            double.IsInfinity(settings.WallSimplificationTolerance) ||
            settings.WallSimplificationTolerance < 0)
        {
            settings.WallSimplificationTolerance = WallSimplificationSettings.DefaultToleranceSceneUnits;
        }

        return settings;
    }
}
