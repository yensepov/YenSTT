using System.Media;
using System.Runtime.InteropServices;
using NAudio.Wave;
using YenSTT.Core;
using YenSTT.Utils;

namespace YenSTT.Audio;

/// <summary>
/// Рекордер аудио с микрофона
/// </summary>
public class AudioRecorder : IDisposable
{
    #region Native Methods for Sound

    [DllImport("user32.dll")]
    private static extern bool MessageBeep(uint type);

    private const uint MB_ICONEXCLAMATION = 0x30;
    private const uint MB_ICONASTERISK = 0x40;

    #endregion

    private WaveInEvent? _waveIn;
    private WaveFileWriter? _waveWriter;
    private readonly AppConfig _config;
    private string? _currentFilePath;
    private DateTime _recordingStart;
    private bool _isRecording;
    private bool _disposed;
    private float _peakLevel;
    private int _silenceFrames;
    private readonly int _silenceThresholdFrames;

    // Параметры записи для локального Whisper
    private const int SampleRate = 16000; // 16 kHz
    private const int Channels = 1; // Mono
    private const int BitsPerSample = 16;

    /// <summary>
    /// Событие начала записи
    /// </summary>
    public event EventHandler? RecordingStarted;

    /// <summary>
    /// Событие окончания записи
    /// </summary>
    public event EventHandler<RecordingCompletedEventArgs>? RecordingCompleted;

    /// <summary>
    /// Событие ошибки записи
    /// </summary>
    public event EventHandler<Exception>? RecordingError;

    /// <summary>
    /// Событие изменения уровня звука
    /// </summary>
    public event EventHandler<float>? AudioLevelChanged;

    /// <summary>
    /// Событие авто-стопа по тишине
    /// </summary>
    public event EventHandler? AutoStopOnSilence;

    /// <summary>
    /// Событие достижения лимита записи
    /// </summary>
    public event EventHandler<int>? RecordingLimitReached;

    /// <summary>
    /// Идёт ли запись
    /// </summary>
    public bool IsRecording => _isRecording;

    /// <summary>
    /// Текущий уровень звука (0.0 - 1.0)
    /// </summary>
    public float PeakLevel => _peakLevel;

    /// <summary>
    /// Текущая длительность записи
    /// </summary>
    public TimeSpan CurrentDuration => DateTime.Now - _recordingStart;

    public AudioRecorder(AppConfig config)
    {
        _config = config;
        _silenceThresholdFrames = config.SilenceThresholdSeconds * (SampleRate / 100); // frames at 100ms buffer
    }

    /// <summary>
    /// Получить список доступных микрофонов
    /// </summary>
    public static List<MicrophoneInfo> GetAvailableMicrophones()
    {
        var devices = new List<MicrophoneInfo>();

        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            try
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                devices.Add(new MicrophoneInfo
                {
                    DeviceId = i.ToString(),
                    Name = capabilities.ProductName,
                    Channels = capabilities.Channels
                });
            }
            catch
            {
                // Игнорируем недоступные устройства
            }
        }

        return devices;
    }

    /// <summary>
    /// Начать запись
    /// </summary>
    public void StartRecording()
    {
        if (_isRecording)
        {
            Logger.Instance.Warning("Попытка начать запись, когда запись уже идёт");
            return;
        }

        try
        {
            // Определяем устройство
            int deviceNumber = 0;
            if (!string.IsNullOrEmpty(_config.SelectedMicrophoneId))
            {
                int.TryParse(_config.SelectedMicrophoneId, out deviceNumber);
            }

            // Создаём файл записи
            CreateAudioFile();

            // Настраиваем захват аудио
            _waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(SampleRate, BitsPerSample, Channels),
                BufferMilliseconds = 100
            };

            // Создаём писатель WAV
            _waveWriter = new WaveFileWriter(_currentFilePath!, _waveIn.WaveFormat);

            // Подписываемся на данные
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.RecordingStopped += OnRecordingStopped;

            // Сбрасываем счётчики
            _peakLevel = 0;
            _silenceFrames = 0;
            _recordingStart = DateTime.Now;

            // Начинаем запись
            _waveIn.StartRecording();
            _isRecording = true;

            Logger.Instance.Info($"Начата запись с микрофона: устройство {deviceNumber}");
            RecordingStarted?.Invoke(this, EventArgs.Empty);

            // Запускаем ограничение длительности
            StartMaxDurationTimer();
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Ошибка начала записи", ex);
            Cleanup();
            RecordingError?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Создать временный WAV-файл.
    /// </summary>
    private void CreateAudioFile()
    {
        _currentFilePath = Path.Combine(
            Path.GetTempPath(),
            $"yenstt_{DateTime.Now:yyyyMMdd_HHmmss_fff}.wav"
        );
    }

    /// <summary>
    /// Остановить запись
    /// </summary>
    public void StopRecording()
    {
        if (!_isRecording || _waveIn == null)
            return;

        try
        {
            _isRecording = false;
            _waveIn.StopRecording();

            var duration = DateTime.Now - _recordingStart;
            Logger.Instance.Info($"Запись остановлена. Длительность: {duration.TotalSeconds:F1} сек");
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Ошибка остановки записи", ex);
            Cleanup();
            RecordingError?.Invoke(this, ex);
        }
    }

    /// <summary>
    /// Обработка данных аудио
    /// </summary>
    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_waveWriter == null)
            return;

        // Записываем данные
        _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);

        // Вычисляем уровень звука
        _peakLevel = CalculatePeakLevel(e.Buffer, e.BytesRecorded);
        AudioLevelChanged?.Invoke(this, _peakLevel);

        // Проверяем тишину для авто-стопа
        if (_config.AutoStopOnSilence && _peakLevel < 0.01f)
        {
            _silenceFrames++;
            if (_silenceFrames >= _silenceThresholdFrames)
            {
                Logger.Instance.Info("Авто-стоп по тишине");
                AutoStopOnSilence?.Invoke(this, EventArgs.Empty);
                StopRecording();
            }
        }
        else
        {
            _silenceFrames = 0;
        }
    }

    /// <summary>
    /// Остановка записи завершена
    /// </summary>
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        var duration = DateTime.Now - _recordingStart;
        var filePath = _currentFilePath;

        // Сначала закрываем writer (финализирует WAV заголовок)
        _waveWriter?.Flush();
        _waveWriter?.Dispose();
        _waveWriter = null;

        // Даём ОС время отпустить файл
        Thread.Sleep(100);

        // Теперь освобождаем остальные ресурсы
        _maxDurationTimer?.Dispose();
        _maxDurationTimer = null;
        _waveIn?.Dispose();
        _waveIn = null;

        if (e.Exception != null)
        {
            Logger.Instance.Error("Ошибка при остановке записи", e.Exception);
            CleanupAudioFile(filePath);
            RecordingError?.Invoke(this, e.Exception);
        }
        else if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                Logger.Instance.Info($"Аудио файл: {fileInfo.Name}, размер: {fileInfo.Length} байт ({fileInfo.Length / 1024.0:F1} KB)");
            }
            catch { }

            filePath = ApplyVad(filePath);

            RecordingCompleted?.Invoke(this, new RecordingCompletedEventArgs
            {
                FilePath = filePath,
                Duration = duration
            });
        }
        else
        {
            Logger.Instance.Warning("Файлы записи не найдены после остановки");
        }
    }

    /// <summary>
    /// Вычислить пиковый уровень звука
    /// </summary>
    private float CalculatePeakLevel(byte[] buffer, int bytesRecorded)
    {
        float max = 0;

        for (int i = 0; i < bytesRecorded; i += 2)
        {
            short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
            float sample32 = sample / 32768f;
            if (sample32 > max) max = sample32;
            if (-sample32 > max) max = -sample32;
        }

        return max;
    }

    /// <summary>
    /// Применить VAD - обрезка хвостовой тишины
    /// </summary>
    private string ApplyVad(string filePath)
    {
        try
        {
            using var reader = new WaveFileReader(filePath);
            var sampleProvider = reader.ToSampleProvider();
            var samples = new List<float>();
            float[] buffer = new float[reader.WaveFormat.SampleRate];
            int read;

            while ((read = sampleProvider.Read(buffer, 0, buffer.Length)) > 0)
            {
                samples.AddRange(buffer.Take(read));
            }

            // Находим последнюю позицию с уровнем выше порога
            int lastSoundIndex = samples.Count - 1;
            const float silenceThreshold = 0.01f;

            for (int i = samples.Count - 1; i >= 0; i--)
            {
                if (Math.Abs(samples[i]) > silenceThreshold)
                {
                    lastSoundIndex = i;
                    break;
                }
            }

            // Добавляем 100мс буфер после последнего звука
            int bufferSamples = reader.WaveFormat.SampleRate / 10;
            int endIndex = Math.Min(lastSoundIndex + bufferSamples, samples.Count);

            // Если обрезали значительную часть, перезаписываем файл
            if (endIndex < samples.Count - bufferSamples)
            {
                var trimmedSamples = samples.Take(endIndex).ToArray();
                var outputPath = Path.Combine(
                    Path.GetDirectoryName(filePath) ?? "",
                    Path.GetFileNameWithoutExtension(filePath) + "_trimmed.wav"
                );

                using var writer = new WaveFileWriter(outputPath, reader.WaveFormat);
                writer.WriteSamples(trimmedSamples, 0, trimmedSamples.Length);

                File.Delete(filePath);

                Logger.Instance.Debug($"VAD: обрезано {(samples.Count - endIndex) / (float)reader.WaveFormat.SampleRate:F2} сек тишины");
                return outputPath;
            }

            return filePath;
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"VAD ошибка: {ex.Message}");
            return filePath;
        }
    }

    /// <summary>
    /// Воспроизвести звуковой сигнал
    /// </summary>
    private void PlayBeepSound()
    {
        try
        {
            // Используем системный звук
            SystemSounds.Exclamation.Play();
        }
        catch
        {
            // Fallback на MessageBeep
            MessageBeep(MB_ICONEXCLAMATION);
        }
    }

    /// <summary>
    /// Таймер максимальной длительности
    /// </summary>
    private System.Threading.Timer? _maxDurationTimer;

    private void StartMaxDurationTimer()
    {
        _maxDurationTimer?.Dispose();
        _maxDurationTimer = new System.Threading.Timer(_ =>
        {
            if (_isRecording)
            {
                var duration = (int)(DateTime.Now - _recordingStart).TotalSeconds;
                Logger.Instance.Info($"Достигнута максимальная длительность записи ({_config.MaxRecordingSeconds} сек)");

                // Воспроизводим сигнал если включено
                if (_config.PlaySoundOnLimit)
                {
                    PlayBeepSound();
                }

                RecordingLimitReached?.Invoke(this, duration);

                StopRecording();
            }
        }, null, _config.MaxRecordingSeconds * 1000, Timeout.Infinite);
    }

    /// <summary>
    /// Очистка ресурсов
    /// </summary>
    private void Cleanup()
    {
        _maxDurationTimer?.Dispose();
        _maxDurationTimer = null;

        _waveWriter?.Dispose();
        _waveWriter = null;

        _waveIn?.Dispose();
        _waveIn = null;
    }

    /// <summary>
    /// Удалить временный аудиофайл при ошибке.
    /// </summary>
    private static void CleanupAudioFile(string? filePath)
    {
        try
        {
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Игнорируем ошибки удаления
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            StopRecording();
            Cleanup();
            _disposed = true;
        }
    }
}

/// <summary>
/// Информация о микрофоне
/// </summary>
public class MicrophoneInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Channels { get; set; }

    public override string ToString() => Name;
}

/// <summary>
/// Аргументы события завершения записи
/// </summary>
public class RecordingCompletedEventArgs : EventArgs
{
    /// <summary>
    /// Путь к временному WAV-файлу.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Длительность записи
    /// </summary>
    public TimeSpan Duration { get; set; }

}
