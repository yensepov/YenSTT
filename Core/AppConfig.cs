using Newtonsoft.Json;

namespace YenSTT.Core;

/// <summary>
/// Режим работы утилиты
/// </summary>
public enum RecordingMode
{
    /// <summary>
    /// Push-to-talk: запись пока нажата клавиша
    /// </summary>
    PushToTalk,

    /// <summary>
    /// Toggle: старт/стоп по нажатию
    /// </summary>
    Toggle
}

/// <summary>
/// Язык распознавания
/// </summary>
public enum RecognitionLanguage
{
    /// <summary>
    /// Автоопределение
    /// </summary>
    Auto,

    /// <summary>
    /// Русский
    /// </summary>
    Russian
}

/// <summary>
/// Режим вставки текста
/// </summary>
public enum InsertionMode
{
    /// <summary>
    /// Через буфер обмена + Ctrl+V (надёжнее)
    /// </summary>
    Clipboard,

    /// <summary>
    /// Эмуляция набора через SendInput (fallback)
    /// </summary>
    SendInput
}

/// <summary>
/// Конфигурация приложения
/// </summary>
public class AppConfig
{
    #region Local Recognition Settings

    /// <summary>
    /// faster-whisper model used for local recognition.
    /// </summary>
    public string WhisperModel { get; set; } = WhisperModels.DefaultName;

    /// <summary>
    /// Язык распознавания
    /// </summary>
    public RecognitionLanguage Language { get; set; } = RecognitionLanguage.Russian;

    /// <summary>
    /// Словарь терминов/подсказки для улучшения распознавания специфичных слов
    /// Пример: "Jira, Confluence, API, JSON, PostgreSQL, Kubernetes"
    /// </summary>
    public string? HintsDictionary { get; set; }

    #endregion

    #region Recording Settings

    /// <summary>
    /// Режим записи
    /// </summary>
    public RecordingMode Mode { get; set; } = RecordingMode.PushToTalk;

    /// <summary>
    /// Максимальная длительность записи в секундах
    /// </summary>
    public int MaxRecordingSeconds { get; set; } = 60;

    /// <summary>
    /// Идентификатор выбранного микрофона
    /// </summary>
    public string? SelectedMicrophoneId { get; set; }

    /// <summary>
    /// Автоматическая остановка при тишине
    /// </summary>
    public bool AutoStopOnSilence { get; set; } = false;

    /// <summary>
    /// Порог тишины в секундах
    /// </summary>
    public int SilenceThresholdSeconds { get; set; } = 3;

    /// <summary>
    /// Воспроизводить звуковой сигнал при достижении лимита записи
    /// </summary>
    public bool PlaySoundOnLimit { get; set; } = true;

    #endregion

    #region Hotkey Settings

    public string HotkeyModifiers { get; set; } = "Ctrl+Alt";
    public string HotkeyKey { get; set; } = "Space";
    [JsonIgnore]
    public string HotkeyDisplay => $"{HotkeyModifiers}+{HotkeyKey}";

    #endregion

    #region Text Processing

    /// <summary>
    /// Включить голосовые команды пунктуации
    /// </summary>
    public bool EnablePunctuationCommands { get; set; } = true;

    #endregion

    #region Insertion Settings

    /// <summary>
    /// Режим вставки текста
    /// </summary>
    public InsertionMode InsertionMode { get; set; } = InsertionMode.Clipboard;

    /// <summary>
    /// Сохранять и восстанавливать буфер обмена
    /// </summary>
    public bool PreserveClipboard { get; set; } = true;

    /// <summary>
    /// Показывать preview при потере фокуса окна
    /// </summary>
    public bool ShowPreviewOnFocusLost { get; set; } = true;

    #endregion

    #region Audio Storage

    public bool SaveAudioFiles { get; set; } = false;
    public string? AudioLogPath { get; set; }

    #endregion

    #region Debug

    public bool DebugMode { get; set; } = false;

    #endregion

    /// <summary>
    /// Получить код языка для локального Whisper
    /// </summary>
    public string GetWhisperLanguageCode()
    {
        return Language switch
        {
            RecognitionLanguage.Russian => "ru",
            RecognitionLanguage.Auto => "",
            _ => "ru"
        };
    }

    /// <summary>
    /// Получить подсказку со словарём терминов для Whisper.
    /// </summary>
    public string GetEffectivePrompt()
    {
        return string.IsNullOrWhiteSpace(HintsDictionary)
            ? string.Empty
            : $"Terms: {HintsDictionary.Trim()}";
    }

    /// <summary>
    /// Создать конфигурацию по умолчанию
    /// </summary>
    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            AudioLogPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "YenSTT",
                "audio_logs"
            )
        };
    }
}
