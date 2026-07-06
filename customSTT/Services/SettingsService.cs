using System.IO;
using System.Text.Json;
using customSTT.Models;

namespace customSTT.Services;

public class SettingsService
{
    private readonly string _settingsPath;

    public SettingsService()
    {
        var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        _settingsPath = Path.Combine(dataDir, "appsettings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return new AppSettings();

            var json = File.ReadAllText(_settingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsPath, json);
    }
}
