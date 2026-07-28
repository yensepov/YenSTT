using System.Drawing;
using YenSTT.API;
using YenSTT.Audio;
using YenSTT.Core;
using YenSTT.Utils;

namespace YenSTT.UI;

/// <summary>
/// Главное приложение с System Tray иконкой
/// </summary>
public class TrayApp : IDisposable
{
    // Скрытая форма с WndProc для получения WM_HOTKEY сообщений
    private class MessageForm : Form
    {
        private HotkeyManager? _hotkeyManager;
        public void SetHotkeyManager(HotkeyManager manager) => _hotkeyManager = manager;
        protected override void WndProc(ref Message m)
        {
            _hotkeyManager?.ProcessMessage(ref m);
            base.WndProc(ref m);
        }
    }

    private readonly ConfigManager _configManager;
    private readonly AppConfig _config;
    private readonly Logger _logger;
    private AudioRecorder? _recorder;
    private WhisperService? _whisperService;
    private TextInserter? _textInserter;
    private readonly HotkeyManager _hotkeyManager;

    private NotifyIcon? _notifyIcon;
    private MessageForm? _messageForm; // Скрытая форма для обработки сообщений Windows
    private bool _isRecording;
    private bool _isProcessing;
    private bool _disposed;

    // Текущий распознанный текст (для preview)
    private string _currentText = string.Empty;

    // Оверлей записи
    private RecordingOverlayForm? _overlay;

    // Иконки
    private Icon? _normalIcon;
    private Icon? _recordingIcon;
    private Icon? _processingIcon;
    private Icon? _errorIcon;

    public TrayApp()
    {
        _configManager = new ConfigManager();
        _config = _configManager.Config;
        _logger = Logger.Instance;
        _logger.SetDebugMode(_config.DebugMode);

        // Создаём скрытую форму для обработки сообщений
        CreateMessageForm();

        _hotkeyManager = new HotkeyManager(_messageForm!.Handle);
        _messageForm.SetHotkeyManager(_hotkeyManager); // подключаем WndProc маршрутизацию

        // Инициализируем компоненты после создания формы
        InitializeComponents();
        SetupEventHandlers();
        RegisterHotkey();

        // Создаём recorder и другие сервисы
        CreateServices();

        // Оверлей записи (не крадёт фокус, показывает статус)
        _overlay = new RecordingOverlayForm();
        _overlay.StopRequested += (s, e) =>
        {
            if (_isRecording)
                _recorder?.StopRecording();
        };
    }

    /// <summary>
    /// Создать сервисы (вызывается после загрузки конфигурации)
    /// </summary>
    private void CreateServices()
    {
        _recorder = new AudioRecorder(_config);
        _whisperService = new WhisperService(_config);
        _whisperService.StatusChanged += (_, status) =>
            InvokeUI(() => _overlay?.ShowProcessing(status));
        _textInserter = new TextInserter(_config);

        // Подписываемся на события recorder
        SetupRecorderEvents();

        // Подписываемся на события text inserter
        SetupTextInserterEvents();
    }

    /// <summary>
    /// Создать скрытую форму для обработки Windows сообщений
    /// </summary>
    private void CreateMessageForm()
    {
        _messageForm = new MessageForm
        {
            ShowInTaskbar = false,
            WindowState = FormWindowState.Minimized,
            FormBorderStyle = FormBorderStyle.None,
            Opacity = 0
        };
        _messageForm.Load += (s, e) => _messageForm.Visible = false;
    }

    /// <summary>
    /// Инициализация компонентов
    /// </summary>
    private void InitializeComponents()
    {
        // Загружаем иконки
        LoadIcons();

        // Создаём контекстное меню
        var contextMenu = CreateContextMenu();

        // Создаём иконку в трее
        _notifyIcon = new NotifyIcon
        {
            Icon = _normalIcon,
            Text = "YenSTT - Готов к работе",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (s, e) => ShowSettings();
    }

    /// <summary>
    /// Загрузить иконки (создаём программно)
    /// </summary>
    private void LoadIcons()
    {
        // Создаём простые иконки программно
        _normalIcon = CreateIcon(Color.FromArgb(0, 120, 215), 'M'); // Микрофон
        _recordingIcon = CreateIcon(Color.Red, '●');
        _processingIcon = CreateIcon(Color.Orange, '◐');
        _errorIcon = CreateIcon(Color.DarkRed, '!');
        _normalIcon = LoadApplicationIcon() ?? _normalIcon;
    }

    /// <summary>
    /// Создать простую иконку
    /// </summary>
    private static Icon? LoadApplicationIcon()
    {
        var applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (applicationIcon == null)
            return null;

        using (applicationIcon)
        {
            return (Icon)applicationIcon.Clone();
        }
    }

    private Icon CreateIcon(Color color, char symbol)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);

        // Фон
        graphics.Clear(Color.Transparent);

        // Круг
        using var brush = new SolidBrush(color);
        graphics.FillEllipse(brush, 2, 2, 28, 28);

        // Символ
        using var font = new Font("Arial", 14, FontStyle.Bold);
        using var whiteBrush = new SolidBrush(Color.White);
        var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };
        graphics.DrawString(symbol.ToString(), font, whiteBrush, new RectangleF(0, 0, 32, 32), sf);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    /// <summary>
    /// Создать контекстное меню
    /// </summary>
    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        // Статус
        var statusItem = menu.Items.Add("Статус: Готов");
        statusItem.Enabled = false;

        menu.Items.Add(new ToolStripSeparator());

        // Запись (для Toggle режима)
        var recordItem = menu.Items.Add("▶ Начать запись");
        recordItem.Click += async (s, e) =>
        {
            if (!_isRecording && !_isProcessing)
            {
                await Task.Delay(300);
                _textInserter?.CaptureTargetWindow();
                _overlay?.ShowRecording();
            }
            ToggleRecording();
        };

        menu.Items.Add(new ToolStripSeparator());

        // Настройки
        var settingsItem = menu.Items.Add("⚙ Настройки");
        settingsItem.Click += (s, e) => ShowSettings();

        // Логи
        var logsItem = menu.Items.Add("📋 Просмотр логов");
        logsItem.Click += (s, e) => ShowLogs();

        menu.Items.Add(new ToolStripSeparator());

        // Справка по командам
        var helpItem = menu.Items.Add("❓ Голосовые команды");
        helpItem.Click += (s, e) => ShowCommandsHelp();

        // О программе
        var aboutItem = menu.Items.Add("ℹ О программе");
        aboutItem.Click += (s, e) => ShowAbout();

        menu.Items.Add(new ToolStripSeparator());

        // Выход
        var exitItem = menu.Items.Add("✕ Выход");
        exitItem.Click += (s, e) => Exit();

        return menu;
    }

    /// <summary>
    /// Настроить обработчики событий
    /// </summary>
    private void SetupEventHandlers()
    {
        // Hotkey события
        _hotkeyManager.HotkeyPressed += OnHotkeyPressed;
        _hotkeyManager.HotkeyConflict += OnHotkeyConflict;
    }

    /// <summary>
    /// Настроить события recorder
    /// </summary>
    private void SetupRecorderEvents()
    {
        if (_recorder == null) return;

        _recorder.RecordingStarted += (s, e) =>
        {
            _isRecording = true;
            UpdateTrayIcon(TrayState.Recording);
            _logger.Info("Запись начата");
        };

        _recorder.RecordingCompleted += OnRecordingCompleted;

        _recorder.RecordingError += (s, ex) =>
        {
            _isRecording = false;
            UpdateTrayIcon(TrayState.Normal);
            ShowNotification("Ошибка записи", ex.Message, ToolTipIcon.Error);
            _logger.Error("Ошибка записи", ex);
        };

        _recorder.AutoStopOnSilence += (s, e) =>
        {
            ShowNotification("Авто-стоп", "Обнаружена тишина", ToolTipIcon.Info, 1500);
        };

        _recorder.RecordingLimitReached += (s, duration) =>
        {
            ShowNotification("Лимит записи", $"Достигнут лимит {duration} сек", ToolTipIcon.Warning, 2000);
        };

    }

    /// <summary>
    /// Настроить события text inserter
    /// </summary>
    private void SetupTextInserterEvents()
    {
        // Статус вставки теперь показывается через оверлей в OnRecordingCompleted.
        // Дублирующие balloon-уведомления убраны.
    }

    /// <summary>
    /// Обработка потери фокуса - показ preview окна
    /// </summary>
    private void HandleFocusLost(FocusCheckResult focusCheck)
    {
        if (string.IsNullOrEmpty(_currentText))
            return;

        var result = PreviewForm.ShowDialog(_currentText, focusCheck, _textInserter!, out var editedText);

        switch (result)
        {
            case PreviewResult.Insert:
                // Вставляем в исходное окно — ждём пока PreviewForm закроется и фокус вернётся
                Task.Run(async () =>
                {
                    await Task.Delay(200);
                    await _textInserter!.PerformInsertionAsync(editedText, focusCheck);
                });
                break;

            case PreviewResult.Copy:
                // Уже скопировано в буфер в PreviewForm
                ShowNotification("Скопировано", "Текст скопирован в буфер обмена", ToolTipIcon.Info, 1500);
                break;

            case PreviewResult.Cancel:
                // Ничего не делаем
                break;
        }
    }

    /// <summary>
    /// Зарегистрировать горячую клавишу
    /// </summary>
    private void RegisterHotkey()
    {
        if (!_hotkeyManager.Register(_config.HotkeyModifiers, _config.HotkeyKey))
        {
            ShowNotification(
                "Ошибка",
                $"Не удалось зарегистрировать горячую клавишу {_config.HotkeyDisplay}",
                ToolTipIcon.Error
            );
        }
    }

    /// <summary>
    /// Обработка нажатия горячей клавиши
    /// </summary>
    private void OnHotkeyPressed(object? sender, HotkeyEventArgs e)
    {
        if (_isProcessing)
        {
            ShowNotification("Подождите", "Идёт обработка предыдущей записи...", ToolTipIcon.Info, 1500);
            return;
        }

        // Захватываем целевое окно прямо сейчас — фокус ещё на нужном окне.
        // RegisterHotKey не генерирует событие KeyUp, поэтому PushToTalk
        // работает как Toggle (нажал — старт, нажал ещё — стоп).
        if (!_isRecording)
        {
            _textInserter?.CaptureTargetWindow();
            _overlay?.ShowRecording();
        }

        ToggleRecording();
    }

    /// <summary>
    /// Переключить запись (для Toggle режима)
    /// </summary>
    private void ToggleRecording()
    {
        if (_isProcessing)
        {
            ShowNotification("Подождите", "Идёт обработка...", ToolTipIcon.Info, 1500);
            return;
        }

        if (_isRecording)
        {
            _recorder?.StopRecording();
        }
        else
        {
            _recorder?.StartRecording();
        }
    }

    /// <summary>
    /// Запись завершена
    /// </summary>
    private async void OnRecordingCompleted(object? sender, RecordingCompletedEventArgs e)
    {
        _isRecording = false;

        var duration = e.Duration.TotalSeconds;
        _logger.Info($"Запись завершена: длительность {duration:F1} сек");
        var audioFilePath = e.FilePath;

        // Слишком короткая запись — отбрасываем
        if (duration < 0.5)
        {
            InvokeUI(() => _overlay?.HideOverlay());
            CleanupAudioFile(audioFilePath);
            return;
        }

        _isProcessing = true;
        InvokeUI(() =>
        {
            UpdateTrayIcon(TrayState.Processing);
            _overlay?.ShowProcessing();
        });

        try
        {
            var result = await _whisperService!.TranscribeAsync(e.FilePath);

            _logger.Info($"Распознано ({result.Language}): \"{result.Text.Substring(0, Math.Min(100, result.Text.Length))}...\"");

            var processedText = TextProcessor.Process(result.Text, _config);
            _currentText = processedText;

            // Вставляем текст (kernel32 fix теперь позволяет ActivateWindow работать)
            bool insertSuccess = false;
            EventHandler<TextInsertionResult> handler = (s, r) => insertSuccess = r.Success;
            _textInserter!.InsertionCompleted += handler;
            try
            {
                await _textInserter!.PerformInsertionAsync(processedText);
            }
            finally
            {
                _textInserter!.InsertionCompleted -= handler;
            }

            if (insertSuccess)
            {
                InvokeUI(() => _overlay?.ShowSuccess());
                await Task.Delay(1200);
            }
            else
            {
                InvokeUI(() => _overlay?.ShowError("Не удалось вставить"));
                await Task.Delay(2000);
            }
            InvokeUI(() => _overlay?.HideOverlay());

            if (_config.SaveAudioFiles)
                SaveAudioFile(audioFilePath, result.Text);
            else
                CleanupAudioFile(audioFilePath);
        }
        catch (WhisperException ex)
        {
            _logger.Error("Ошибка локального Whisper", ex);
            InvokeUI(() => _overlay?.ShowError("Ошибка распознавания"));
            await Task.Delay(2000);
            InvokeUI(() => _overlay?.HideOverlay());
            CleanupAudioFile(audioFilePath);
        }
        catch (Exception ex)
        {
            _logger.Error("Неожиданная ошибка", ex);
            InvokeUI(() => _overlay?.ShowError("Ошибка"));
            await Task.Delay(2000);
            InvokeUI(() => _overlay?.HideOverlay());
            CleanupAudioFile(audioFilePath);
        }
        finally
        {
            _isProcessing = false;
            InvokeUI(() => UpdateTrayIcon(TrayState.Normal));
        }
    }

    /// <summary>
    /// Выполнить действие в UI-потоке (безопасно из фоновых потоков)
    /// </summary>
    private void InvokeUI(Action action)
    {
        if (_messageForm != null && _messageForm.InvokeRequired)
            _messageForm.Invoke(action);
        else
            action();
    }

    /// <summary>
    /// Конфликт горячей клавиши
    /// </summary>
    private void OnHotkeyConflict(object? sender, string message)
    {
        ShowNotification("Горячая клавиша занята", message, ToolTipIcon.Warning);
    }

    /// <summary>
    /// Состояние иконки в трее
    /// </summary>
    private enum TrayState
    {
        Normal,
        Recording,
        Processing,
        Error
    }

    /// <summary>
    /// Обновить иконку в трее
    /// </summary>
    private void UpdateTrayIcon(TrayState state)
    {
        _notifyIcon!.Icon = state switch
        {
            TrayState.Recording => _recordingIcon,
            TrayState.Processing => _processingIcon,
            TrayState.Error => _errorIcon,
            _ => _normalIcon
        };

        _notifyIcon.Text = state switch
        {
            TrayState.Recording => "YenSTT - Запись...",
            TrayState.Processing => "YenSTT - Распознавание...",
            TrayState.Error => "YenSTT - Ошибка",
            _ => "YenSTT - Готов к работе"
        };
    }

    /// <summary>
    /// Показать уведомление
    /// </summary>
    private void ShowNotification(string title, string text, ToolTipIcon icon, int timeout = 5000)
    {
        _notifyIcon?.ShowBalloonTip(timeout, title, text, icon);
    }

    /// <summary>
    /// Сохранить аудио и транскрипт.
    /// </summary>
    private void SaveAudioFile(string filePath, string transcript)
    {
        try
        {
            var logDir = _configManager.GetAudioLogDirectory();
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            // Сохраняем транскрипт
            var transcriptPath = Path.Combine(logDir, $"audio_{timestamp}.txt");
            File.WriteAllText(transcriptPath, transcript);

            if (File.Exists(filePath))
            {
                var newFileName = $"audio_{timestamp}.wav";
                var newPath = Path.Combine(logDir, newFileName);
                File.Move(filePath, newPath);
            }

            _logger.Info($"Аудио сохранено: {transcriptPath}");
        }
        catch (Exception ex)
        {
            _logger.Error("Ошибка сохранения аудио", ex);
        }
    }

    /// <summary>
    /// Удалить временный аудиофайл.
    /// </summary>
    private static void CleanupAudioFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
        catch
        {
            // Игнорируем ошибки очистки
        }
    }

    /// <summary>
    /// Показать настройки
    /// </summary>
    private void ShowSettings()
    {
        using var form = new SettingsForm(_configManager);
        form.ShowDialog();

        // Обновляем настройки после закрытия формы
        _logger.SetDebugMode(_config.DebugMode);

        // Пересоздаём сервисы с новыми настройками
        _recorder?.Dispose();
        _whisperService?.Dispose();
        CreateServices();

        // Перерегистрируем хоткей
        _hotkeyManager.Unregister();
        RegisterHotkey();
    }

    /// <summary>
    /// Показать логи
    /// </summary>
    private void ShowLogs()
    {
        var form = new LogViewerForm();
        form.Show();
    }

    /// <summary>
    /// Показать справку по командам
    /// </summary>
    private void ShowCommandsHelp()
    {
        MessageBox.Show(
            TextProcessor.GetCommandsHelp(),
            "Голосовые команды пунктуации",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    /// <summary>
    /// Показать информацию о программе
    /// </summary>
    private void ShowAbout()
    {
        MessageBox.Show(
            "YenSTT - Локальная утилита диктовки\n\n" +
            $"Версия: {Application.ProductVersion}\n\n" +
            $"Движок: faster-whisper (локально)\n" +
            "Модель: small / CPU int8\n" +
            $"Горячая клавиша: {_config.HotkeyDisplay}\n" +
            $"Режим: {_config.Mode}\n\n" +
            "Аудио не покидает компьютер.",
            "О программе YenSTT",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
    }

    /// <summary>
    /// Выход из приложения
    /// </summary>
    private void Exit()
    {
        if (_isRecording)
        {
            var result = MessageBox.Show(
                "Идёт запись. Прервать и выйти?",
                "Подтверждение",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (result != DialogResult.Yes)
                return;

            _recorder?.StopRecording();
        }

        Application.Exit();
    }

    /// <summary>
    /// Запустить приложение
    /// </summary>
    public void Run()
    {
        Application.Run(_messageForm!);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _overlay?.Dispose();
            _recorder?.Dispose();
            _whisperService?.Dispose();
            _hotkeyManager?.Dispose();
            _notifyIcon?.Dispose();
            _normalIcon?.Dispose();
            _recordingIcon?.Dispose();
            _processingIcon?.Dispose();
            _errorIcon?.Dispose();
            _messageForm?.Dispose();
            _logger?.Dispose();
            _disposed = true;
        }
    }
}
