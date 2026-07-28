using Newtonsoft.Json;

namespace YenSTT.Core;

/// <summary>
/// Менеджер конфигурации приложения
/// </summary>
public class ConfigManager
{
    private static readonly string AppDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "YenSTT"
    );

    private static readonly string LegacyAppDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "LocalSTT"
    );

    private static readonly string ConfigPath = Path.Combine(
        AppDirectory,
        "config.json"
    );

    private readonly AppConfig _config;

    public ConfigManager()
    {
        MigrateLegacyData();
        _config = LoadOrCreateConfig();
    }

    public AppConfig Config => _config;

    /// <summary>
    /// Загрузить или создать конфигурацию
    /// </summary>
    private AppConfig LoadOrCreateConfig()
    {
        EnsureDirectoryExists();

        if (File.Exists(ConfigPath))
        {
            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonConvert.DeserializeObject<AppConfig>(json);
                if (config == null)
                    return AppConfig.CreateDefault();

                // Cloud releases stored "whisper-1" here; YenSTT now supports local small only.
                if (!string.Equals(config.WhisperModel, "small", StringComparison.OrdinalIgnoreCase))
                {
                    config.WhisperModel = "small";
                    File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(config, Formatting.Indented));
                }

                var legacyAudioPath = Path.Combine(LegacyAppDirectory, "audio_logs");
                if (string.Equals(config.AudioLogPath, legacyAudioPath, StringComparison.OrdinalIgnoreCase))
                    config.AudioLogPath = Path.Combine(AppDirectory, "audio_logs");

                return config;
            }
            catch
            {
                return AppConfig.CreateDefault();
            }
        }

        return AppConfig.CreateDefault();
    }

    /// <summary>
    /// Сохранить конфигурацию
    /// </summary>
    public void Save()
    {
        EnsureDirectoryExists();

        var json = JsonConvert.SerializeObject(_config, Formatting.Indented);
        File.WriteAllText(ConfigPath, json);
    }

    /// <summary>
    /// Создать директорию для конфигурации
    /// </summary>
    private static void EnsureDirectoryExists()
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    private static void MigrateLegacyData()
    {
        if (!Directory.Exists(LegacyAppDirectory))
            return;

        try
        {
            CopyDirectory(LegacyAppDirectory, AppDirectory);
        }
        catch
        {
            // Migration is best-effort; a fresh configuration remains usable.
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var destinationFile = Path.Combine(destination, Path.GetFileName(file));
            if (!File.Exists(destinationFile))
                File.Copy(file, destinationFile);
        }

        foreach (var directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }

    /// <summary>
    /// Получить путь к папке логов
    /// </summary>
    public static string GetLogDirectory()
    {
        return Path.Combine(
            AppDirectory,
            "logs"
        );
    }

    /// <summary>
    /// Получить путь к папке аудио логов
    /// </summary>
    public string GetAudioLogDirectory()
    {
        var path = _config.AudioLogPath;
        if (string.IsNullOrEmpty(path))
        {
            path = Path.Combine(
                AppDirectory,
                "audio_logs"
            );
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }

        return path;
    }
}
