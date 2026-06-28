using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Zass.Core.Settings;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> as JSON (Architecture §10). The default
/// location is <c>%APPDATA%\Zass\settings.json</c>; the path is injectable so the
/// store can be unit-tested against a temporary file.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // Enums as readable strings ("Spanish", "Png") so the file stays editable.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _path;

    public SettingsStore(string path) => _path = path;

    /// <summary>The default settings file under the per-user roaming app-data folder.</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Zass",
        "settings.json");

    /// <summary>Creates a store backed by the default <see cref="DefaultPath"/>.</summary>
    public static SettingsStore CreateDefault() => new(DefaultPath);

    /// <summary>
    /// Reads the settings file. A missing file yields defaults; a corrupt or
    /// unreadable file is logged and also falls back to defaults so startup never
    /// fails because of bad persisted state. Values are normalized before returning.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            string json = File.ReadAllText(_path);
            AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Trace.TraceWarning($"Zass: could not read settings ('{_path}'), using defaults: {ex.Message}");
            return new AppSettings();
        }
    }

    /// <summary>
    /// Writes the settings as JSON, creating the parent folder if needed. Values are
    /// normalized first. I/O errors propagate so the caller can decide how to surface
    /// them (a failed settings write is non-fatal but must not be swallowed silently).
    /// </summary>
    public void Save(AppSettings settings)
    {
        settings.Normalize();

        string? directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_path, json);
    }
}
