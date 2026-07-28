using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using YenSTT.Core;
using YenSTT.Utils;

namespace YenSTT.API;

public class WhisperResponse
{
    public string Text { get; set; } = string.Empty;
    public string? Language { get; set; }
    public float? Duration { get; set; }
}

/// <summary>
/// Runs faster-whisper locally through a persistent Python worker.
/// </summary>
public sealed class WhisperService : IDisposable
{
    private readonly AppConfig _config;
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private readonly ConcurrentQueue<string> _stderr = new();
    private Process? _worker;
    private bool _disposed;

    public event EventHandler<string>? StatusChanged;

    public WhisperService(AppConfig config)
    {
        _config = config;
    }

    public async Task<WhisperResponse> TranscribeAsync(string audioFilePath)
    {
        if (!File.Exists(audioFilePath))
            throw new WhisperException($"Аудиофайл не найден: {audioFilePath}");

        var fileInfo = new FileInfo(audioFilePath);
        if (fileInfo.Length < 1024)
            throw new WhisperException($"Аудиофайл слишком маленький ({fileInfo.Length} байт).");

        StatusChanged?.Invoke(
            this,
            IsModelCached()
                ? "Локальное распознавание (faster-whisper small)..."
                : "Скачивание модели small (~464 МБ), затем распознавание..."
        );
        Logger.Instance.Info(
            $"Local Whisper: файл {fileInfo.Name}, {fileInfo.Length / 1024.0:F1} KB, " +
            $"модель={_config.WhisperModel}, язык={_config.GetWhisperLanguageCode()}"
        );

        var startedAt = Stopwatch.StartNew();
        var request = new WorkerRequest
        {
            Command = "transcribe",
            AudioPath = Path.GetFullPath(audioFilePath),
            Model = _config.WhisperModel,
            CacheDir = GetModelCacheDirectory(),
            Language = NullIfEmpty(_config.GetWhisperLanguageCode()),
            Prompt = NullIfEmpty(_config.GetEffectivePrompt())
        };

        var response = await SendRequestAsync(request, TimeSpan.FromSeconds(_config.LocalWhisperTimeoutSeconds));
        if (string.IsNullOrWhiteSpace(response.Text))
            throw new WhisperException("Речь не распознана. Проверьте микрофон и длительность записи.");

        Logger.Instance.Info($"Local Whisper завершён за {startedAt.Elapsed.TotalSeconds:F1} сек.");
        return new WhisperResponse
        {
            Text = response.Text.Trim(),
            Language = response.Language,
            Duration = response.Duration
        };
    }

    public async Task<WhisperResponse> TranscribeChunksAsync(List<string> audioFilePaths)
    {
        if (audioFilePaths.Count == 0)
            throw new WhisperException("Нет аудиофайлов для обработки.");
        if (audioFilePaths.Count == 1)
            return await TranscribeAsync(audioFilePaths[0]);

        var texts = new List<string>();
        var totalDuration = 0f;
        string? language = null;

        for (var index = 0; index < audioFilePaths.Count; index++)
        {
            StatusChanged?.Invoke(this, $"Локальное распознавание {index + 1}/{audioFilePaths.Count}...");
            var result = await TranscribeAsync(audioFilePaths[index]);
            texts.Add(result.Text);
            totalDuration += result.Duration ?? 0;
            language ??= result.Language;
        }

        return new WhisperResponse
        {
            Text = NormalizeChunkJoin(string.Join(" ", texts)),
            Language = language,
            Duration = totalDuration
        };
    }

    public async Task<string> CheckAvailabilityAsync()
    {
        var response = await SendRequestAsync(
            new WorkerRequest { Command = "ping" },
            TimeSpan.FromSeconds(60)
        );
        return response.Version ?? "установлен";
    }

    private async Task<WorkerResponse> SendRequestAsync(WorkerRequest request, TimeSpan timeout)
    {
        await _requestLock.WaitAsync();
        try
        {
            ThrowIfDisposed();
            EnsureWorkerStarted();

            var json = JsonConvert.SerializeObject(request, Formatting.None);
            await _worker!.StandardInput.WriteLineAsync(json);
            await _worker.StandardInput.FlushAsync();

            string? line;
            try
            {
                line = await _worker.StandardOutput.ReadLineAsync().WaitAsync(timeout);
            }
            catch (TimeoutException)
            {
                StopWorker();
                throw new WhisperException(
                    "Локальное распознавание превысило время ожидания. " +
                    "При первом запуске модель small может долго скачиваться."
                );
            }

            if (line == null)
            {
                var details = GetRecentStderr();
                StopWorker();
                throw new WhisperException(
                    "Процесс faster-whisper неожиданно завершился." +
                    (string.IsNullOrEmpty(details) ? string.Empty : $" {details}")
                );
            }

            var response = JsonConvert.DeserializeObject<WorkerResponse>(line)
                ?? throw new WhisperException("Некорректный ответ локального распознавателя.");

            if (!response.Ok)
                throw new WhisperException(response.Error ?? "Неизвестная ошибка faster-whisper.");

            return response;
        }
        catch (WhisperException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Ошибка локального faster-whisper", ex);
            throw new WhisperException($"Ошибка локального распознавания: {ex.Message}", ex);
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private void EnsureWorkerStarted()
    {
        if (_worker is { HasExited: false })
            return;

        StopWorker();
        var runtimePath = Path.Combine(AppContext.BaseDirectory, "Runtime", "faster-whisper-worker.exe");
        var scriptPath = Path.Combine(AppContext.BaseDirectory, "Python", "faster_whisper_worker.py");
        var candidates = new List<(string FileName, string? LauncherArgument, bool UsesPython)>();
        if (File.Exists(runtimePath))
            candidates.Add((runtimePath, null, false));
        if (!string.IsNullOrWhiteSpace(_config.PythonExecutable))
            candidates.Add((_config.PythonExecutable.Trim(), null, true));
        candidates.Add(("py", "-3", true));
        candidates.Add(("python", null, true));

        Exception? lastError = null;
        foreach (var candidate in candidates.Distinct())
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = candidate.FileName,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                };
                if (candidate.UsesPython)
                {
                    if (!File.Exists(scriptPath))
                        continue;
                    if (candidate.LauncherArgument != null)
                        startInfo.ArgumentList.Add(candidate.LauncherArgument);
                    startInfo.ArgumentList.Add("-u");
                    startInfo.ArgumentList.Add(scriptPath);
                }
                startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
                startInfo.Environment["HF_HUB_DISABLE_SYMLINKS_WARNING"] = "1";

                var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                if (!process.Start())
                    throw new InvalidOperationException("Python process did not start.");

                process.ErrorDataReceived += (sender, eventArgs) =>
                {
                    if (string.IsNullOrWhiteSpace(eventArgs.Data))
                        return;
                    _stderr.Enqueue(eventArgs.Data);
                    while (_stderr.Count > 20)
                        _stderr.TryDequeue(out var _);
                    Logger.Instance.Debug($"faster-whisper: {eventArgs.Data}");
                };
                process.BeginErrorReadLine();
                _worker = process;
                Logger.Instance.Info($"Запущен локальный faster-whisper worker: {candidate.FileName}");
                return;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new WhisperException(
            "Python 3 не найден. Установите Python и запустите install-local-whisper.bat.",
            lastError ?? new InvalidOperationException("Python 3 is unavailable.")
        );
    }

    private static string GetModelCacheDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YenSTT",
            "models"
        );
    }

    private static bool IsModelCached()
    {
        var cacheDirectory = GetModelCacheDirectory();
        return Directory.Exists(cacheDirectory) &&
            Directory.EnumerateFiles(cacheDirectory, "model.bin", SearchOption.AllDirectories).Any();
    }

    private static string NormalizeChunkJoin(string text)
    {
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+([.,!?;:)])", "$1");
        return text.Trim();
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private string GetRecentStderr() => string.Join(" ", _stderr.ToArray().TakeLast(5));

    private void StopWorker()
    {
        if (_worker == null)
            return;

        try
        {
            if (!_worker.HasExited)
                _worker.Kill(entireProcessTree: true);
        }
        catch
        {
            // Process may already be gone.
        }
        finally
        {
            _worker.Dispose();
            _worker = null;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        StopWorker();
        _requestLock.Dispose();
    }

    private sealed class WorkerRequest
    {
        [JsonProperty("command")]
        public string Command { get; set; } = string.Empty;

        [JsonProperty("audio_path", NullValueHandling = NullValueHandling.Ignore)]
        public string? AudioPath { get; set; }

        [JsonProperty("model", NullValueHandling = NullValueHandling.Ignore)]
        public string? Model { get; set; }

        [JsonProperty("cache_dir", NullValueHandling = NullValueHandling.Ignore)]
        public string? CacheDir { get; set; }

        [JsonProperty("language", NullValueHandling = NullValueHandling.Ignore)]
        public string? Language { get; set; }

        [JsonProperty("prompt", NullValueHandling = NullValueHandling.Ignore)]
        public string? Prompt { get; set; }
    }

    private sealed class WorkerResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("error")]
        public string? Error { get; set; }

        [JsonProperty("text")]
        public string? Text { get; set; }

        [JsonProperty("language")]
        public string? Language { get; set; }

        [JsonProperty("duration")]
        public float? Duration { get; set; }

        [JsonProperty("version")]
        public string? Version { get; set; }
    }
}

public class WhisperException : Exception
{
    public WhisperException(string message) : base(message) { }
    public WhisperException(string message, Exception inner) : base(message, inner) { }
}
