using System.Collections.Concurrent;

namespace YenSTT.Utils;

/// <summary>
/// Уровень логирования
/// </summary>
public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Логгер с ротацией файлов
/// </summary>
public class Logger : IDisposable
{
    private static Logger? _instance;
    private static readonly object _lock = new();

    private readonly string _logDirectory;
    private readonly int _maxFileSize;
    private readonly int _maxFileCount;
    private readonly ConcurrentQueue<string> _queue;
    private readonly Thread _writerThread;
    private readonly AutoResetEvent _writeEvent;
    private bool _disposed;
    private bool _debugMode;
    private string _currentLogFile = string.Empty;

    /// <summary>
    /// Получить singleton экземпляр логгера
    /// </summary>
    public static Logger Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new Logger();
                }
            }
            return _instance;
        }
    }

    private Logger()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YenSTT",
            "logs"
        );

        _maxFileSize = 2 * 1024 * 1024; // 2 MB
        _maxFileCount = 5;
        _queue = new ConcurrentQueue<string>();
        _writeEvent = new AutoResetEvent(false);

        EnsureLogDirectory();
        RotateIfNeeded();

        _writerThread = new Thread(WriteLoop)
        {
            IsBackground = true,
            Name = "LoggerThread"
        };
        _writerThread.Start();
    }

    /// <summary>
    /// Установить режим отладки
    /// </summary>
    public void SetDebugMode(bool enabled)
    {
        _debugMode = enabled;
    }

    /// <summary>
    /// Залогировать сообщение
    /// </summary>
    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        if (level == LogLevel.Debug && !_debugMode)
            return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var levelStr = level.ToString().ToUpper().PadRight(7);
        var entry = $"[{timestamp}] [{levelStr}] {message}";

        if (exception != null)
        {
            entry += $"\n{exception.GetType().Name}: {exception.Message}";
            if (_debugMode && exception.StackTrace != null)
            {
                entry += $"\n{exception.StackTrace}";
            }
        }

        _queue.Enqueue(entry);
        _writeEvent.Set();
    }

    public void Debug(string message) => Log(LogLevel.Debug, message);
    public void Info(string message) => Log(LogLevel.Info, message);
    public void Warning(string message) => Log(LogLevel.Warning, message);
    public void Error(string message, Exception? ex = null) => Log(LogLevel.Error, message, ex);

    /// <summary>
    /// Цикл записи в файл
    /// </summary>
    private void WriteLoop()
    {
        while (!_disposed)
        {
            _writeEvent.WaitOne(1000);

            while (_queue.TryDequeue(out var entry))
            {
                try
                {
                    WriteEntry(entry);
                }
                catch
                {
                    // Игнорируем ошибки записи
                }
            }
        }
    }

    /// <summary>
    /// Записать строку в лог-файл
    /// </summary>
    private void WriteEntry(string entry)
    {
        if (string.IsNullOrEmpty(_currentLogFile))
        {
            _currentLogFile = GetTodaysLogFile();
        }

        // Проверяем размер файла
        if (File.Exists(_currentLogFile))
        {
            var info = new FileInfo(_currentLogFile);
            if (info.Length >= _maxFileSize)
            {
                RotateLogFiles();
                _currentLogFile = GetTodaysLogFile();
            }
        }

        File.AppendAllText(_currentLogFile, entry + "\n");
    }

    /// <summary>
    /// Получить путь к лог-файлу на сегодня
    /// </summary>
    private string GetTodaysLogFile()
    {
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        return Path.Combine(_logDirectory, $"log_{date}.txt");
    }

    /// <summary>
    /// Проверить и выполнить ротацию при старте
    /// </summary>
    private void RotateIfNeeded()
    {
        var files = GetLogFiles();
        if (files.Count >= _maxFileCount)
        {
            // Удаляем самые старые
            var toDelete = files.Skip(_maxFileCount - 1).ToList();
            foreach (var file in toDelete)
            {
                try { File.Delete(file); } catch { }
            }
        }
    }

    /// <summary>
    /// Выполнить ротацию лог-файлов
    /// </summary>
    private void RotateLogFiles()
    {
        var files = GetLogFiles();

        // Удаляем самые старые если достигнут лимит
        while (files.Count >= _maxFileCount)
        {
            try
            {
                File.Delete(files[files.Count - 1]);
                files.RemoveAt(files.Count - 1);
            }
            catch
            {
                break;
            }
        }
    }

    /// <summary>
    /// Получить список лог-файлов (от новых к старым)
    /// </summary>
    private List<string> GetLogFiles()
    {
        if (!Directory.Exists(_logDirectory))
            return new List<string>();

        return Directory.GetFiles(_logDirectory, "log_*.txt")
            .OrderByDescending(f => f)
            .ToList();
    }

    /// <summary>
    /// Создать директорию для логов
    /// </summary>
    private void EnsureLogDirectory()
    {
        if (!Directory.Exists(_logDirectory))
        {
            Directory.CreateDirectory(_logDirectory);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _writeEvent.Set();
        _writerThread.Join(1000);
    }
}
