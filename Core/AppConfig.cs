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
    /// Локальная faster-whisper модель.
    /// </summary>
    public string WhisperModel { get; set; } = "small";

    /// <summary>
    /// Необязательный путь к python.exe. Пустое значение включает автоопределение.
    /// </summary>
    public string PythonExecutable { get; set; } = string.Empty;

    /// <summary>
    /// Язык распознавания
    /// </summary>
    public RecognitionLanguage Language { get; set; } = RecognitionLanguage.Russian;

    /// <summary>
    /// Подсказка/контекст для улучшения распознавания (prompt)
    /// </summary>
    public string? WhisperPrompt { get; set; }

    /// <summary>
    /// Словарь терминов/подсказки для улучшения распознавания специфичных слов
    /// Пример: "Jira, Confluence, API, JSON, PostgreSQL, Kubernetes"
    /// </summary>
    public string? HintsDictionary { get; set; }

    /// <summary>
    /// Таймаут локального распознавания в секундах. Включает первое скачивание модели.
    /// </summary>
    public int LocalWhisperTimeoutSeconds { get; set; } = 900;

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

    /// <summary>
    /// Включить режим чанков (разбиение длинных записей)
    /// </summary>
    public bool EnableChunking { get; set; } = false;

    /// <summary>
    /// Размер чанка в секундах (для длинных записей)
    /// </summary>
    public int ChunkSizeSeconds { get; set; } = 60;

    #endregion

    #region Hotkey Settings

    public string HotkeyModifiers { get; set; } = "Ctrl+Alt";
    public string HotkeyKey { get; set; } = "Space";
    public string HotkeyDisplay => $"{HotkeyModifiers}+{HotkeyKey}";

    #endregion

    #region Text Processing

    /// <summary>
    /// Включить голосовые команды пунктуации
    /// </summary>
    public bool EnablePunctuationCommands { get; set; } = true;

    /// <summary>
    /// Вставлять как plain text
    /// </summary>
    public bool InsertAsPlainText { get; set; } = true;

    /// <summary>
    /// Добавить пробел перед текстом
    /// </summary>
    public bool AddSpaceBefore { get; set; } = false;

    /// <summary>
    /// Добавить пробел после текста
    /// </summary>
    public bool AddSpaceAfter { get; set; } = false;

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

    /// <summary>
    /// Автоматически копировать в буфер если фокус потерян
    /// </summary>
    public bool AutoCopyOnFocusLost { get; set; } = false;

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
    /// Получить полный prompt для Whisper (объединяет prompt и словарь терминов)
    /// </summary>
    public string GetEffectivePrompt()
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(WhisperPrompt))
        {
            parts.Add(WhisperPrompt.Trim());
        }

        if (!string.IsNullOrWhiteSpace(HintsDictionary))
        {
            parts.Add($"Terms: {HintsDictionary.Trim()}");
        }

        return parts.Count > 0 ? string.Join(" ", parts) : string.Empty;
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
