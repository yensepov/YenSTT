using YenSTT.Audio;
using YenSTT.Core;

namespace YenSTT.UI;

/// <summary>
/// Форма настроек приложения
/// </summary>
public class SettingsForm : Form
{
    private readonly ConfigManager _configManager;
    private readonly AppConfig _config;

    // Local recognition controls
    private TextBox? _whisperModelTextBox;
    private TextBox? _hintsTextBox;
    private NumericUpDown? _localTimeoutNumeric;

    // Recording Controls
    private ComboBox? _languageComboBox;
    private ComboBox? _modeComboBox;
    private ComboBox? _microphoneComboBox;
    private NumericUpDown? _maxDurationNumeric;
    private CheckBox? _autoStopSilenceCheckBox;
    private NumericUpDown? _silenceThresholdNumeric;
    private CheckBox? _playSoundOnLimitCheckBox;
    private CheckBox? _enableChunkingCheckBox;
    private NumericUpDown? _chunkSizeNumeric;

    // Hotkey Controls
    private ComboBox? _hotkeyModifiersComboBox;
    private ComboBox? _hotkeyKeyComboBox;

    // Insertion Controls
    private ComboBox? _insertionModeComboBox;
    private CheckBox? _preserveClipboardCheckBox;
    private CheckBox? _showPreviewOnFocusLostCheckBox;

    // Text Processing Controls
    private CheckBox? _punctuationCommandsCheckBox;

    // Storage Controls
    private CheckBox? _saveAudioCheckBox;
    private TextBox? _audioLogPathTextBox;

    // Debug
    private CheckBox? _debugModeCheckBox;

    // Buttons
    private Button? _saveButton;
    private Button? _cancelButton;
    private Button? _testLocalButton;
    private Label? _statusLabel;

    public SettingsForm(ConfigManager configManager)
    {
        _configManager = configManager;
        _config = configManager.Config;

        InitializeComponents();
        LoadSettings();
    }

    private void InitializeComponents()
    {
        Text = "Настройки YenSTT";
        Size = new Size(520, 830);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScroll = true;

        var mainPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
        };

        var y = 10;
        const int rowHeight = 26;
        const int spacing = 6;
        const int groupWidth = 480;

        // === Local Recognition Group ===
        var localGroup = CreateGroupBox("Локальное распознавание", 10, y, groupWidth, 170);

        CreateLabel("Модель:", 15, 22, 100, localGroup);
        _whisperModelTextBox = new TextBox
        {
            Location = new Point(115, 19),
            Size = new Size(120, 23),
            Text = "small",
            ReadOnly = true,
            Parent = localGroup
        };

        var modeLabel = new Label
        {
            Text = "CPU · int8 · аудио остаётся на компьютере",
            Location = new Point(245, 22),
            Size = new Size(215, 20),
            ForeColor = Color.DarkGreen,
            Parent = localGroup
        };

        CreateLabel("Движок:", 15, 52, 100, localGroup);
        var runtimeTextBox = new TextBox
        {
            Location = new Point(115, 49),
            Size = new Size(240, 23),
            Text = "Встроенный faster-whisper 1.2.1",
            ReadOnly = true,
            Parent = localGroup
        };

        _testLocalButton = new Button
        {
            Text = "Проверить",
            Location = new Point(365, 48),
            Size = new Size(90, 25),
            Parent = localGroup
        };
        _testLocalButton.Click += TestLocalButton_Click;

        CreateLabel("Таймаут (сек):", 15, 82, 100, localGroup);
        _localTimeoutNumeric = new NumericUpDown
        {
            Location = new Point(115, 79),
            Size = new Size(80, 23),
            Minimum = 60,
            Maximum = 1800,
            Value = 900,
            Parent = localGroup
        };

        CreateLabel("Словарь терминов:", 15, 112, 100, localGroup);
        _hintsTextBox = new TextBox
        {
            Location = new Point(115, 109),
            Size = new Size(340, 23),
            PlaceholderText = "Jira, Confluence, API, JSON...",
            Parent = localGroup
        };

        var hintsHelpLabel = new Label
        {
            Text = "Термины для улучшения распознавания (через запятую)",
            Location = new Point(115, 136),
            Size = new Size(340, 18),
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8),
            Parent = localGroup
        };

        mainPanel.Controls.Add(localGroup);
        y += 180;

        // === Recording Settings Group ===
        var recGroup = CreateGroupBox("Настройки записи", 10, y, groupWidth, 175);
        var recY = 22;

        CreateLabel("Язык:", 15, recY, 100, recGroup);
        _languageComboBox = new ComboBox
        {
            Location = new Point(115, recY - 3),
            Size = new Size(150, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = recGroup
        };
        _languageComboBox.Items.AddRange(new object[] { "Русский", "Автоопределение" });

        recY += rowHeight + spacing;

        CreateLabel("Режим:", 15, recY, 100, recGroup);
        _modeComboBox = new ComboBox
        {
            Location = new Point(115, recY - 3),
            Size = new Size(150, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = recGroup
        };
        _modeComboBox.Items.AddRange(new object[] { "Push-to-talk", "Toggle" });

        recY += rowHeight + spacing;

        CreateLabel("Микрофон:", 15, recY, 100, recGroup);
        _microphoneComboBox = new ComboBox
        {
            Location = new Point(115, recY - 3),
            Size = new Size(340, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = recGroup
        };
        LoadMicrophones();

        recY += rowHeight + spacing;

        CreateLabel("Макс. длит. (сек):", 15, recY, 100, recGroup);
        _maxDurationNumeric = new NumericUpDown
        {
            Location = new Point(115, recY - 3),
            Size = new Size(70, 23),
            Minimum = 5,
            Maximum = 300,
            Value = 60,
            Parent = recGroup
        };

        _playSoundOnLimitCheckBox = new CheckBox
        {
            Text = "Звук при лимите",
            Location = new Point(195, recY - 3),
            Size = new Size(130, 23),
            Parent = recGroup
        };

        _autoStopSilenceCheckBox = new CheckBox
        {
            Text = "Авто-стоп по тишине",
            Location = new Point(330, recY - 3),
            Size = new Size(140, 23),
            Parent = recGroup
        };
        _autoStopSilenceCheckBox.CheckedChanged += (s, e) =>
            _silenceThresholdNumeric!.Enabled = _autoStopSilenceCheckBox.Checked;

        recY += rowHeight + spacing;

        CreateLabel("Порог тишины (сек):", 15, recY, 100, recGroup);
        _silenceThresholdNumeric = new NumericUpDown
        {
            Location = new Point(115, recY - 3),
            Size = new Size(70, 23),
            Minimum = 1,
            Maximum = 30,
            Value = 3,
            Enabled = false,
            Parent = recGroup
        };

        _enableChunkingCheckBox = new CheckBox
        {
            Text = "Чанки для длинных записей",
            Location = new Point(195, recY - 3),
            Size = new Size(180, 23),
            Parent = recGroup
        };
        _enableChunkingCheckBox.CheckedChanged += (s, e) =>
            _chunkSizeNumeric!.Enabled = _enableChunkingCheckBox.Checked;

        recY += rowHeight + spacing;

        CreateLabel("Размер чанка (сек):", 15, recY, 100, recGroup);
        _chunkSizeNumeric = new NumericUpDown
        {
            Location = new Point(115, recY - 3),
            Size = new Size(70, 23),
            Minimum = 30,
            Maximum = 300,
            Value = 60,
            Enabled = false,
            Parent = recGroup
        };

        mainPanel.Controls.Add(recGroup);
        y += 185;

        // === Hotkey Settings Group ===
        var hotkeyGroup = CreateGroupBox("Горячая клавиша", 10, y, groupWidth, 55);
        var hkY = 22;

        CreateLabel("Модификаторы:", 15, hkY, 100, hotkeyGroup);
        _hotkeyModifiersComboBox = new ComboBox
        {
            Location = new Point(115, hkY - 3),
            Size = new Size(130, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = hotkeyGroup
        };
        _hotkeyModifiersComboBox.Items.AddRange(new object[]
        {
            "Ctrl+Alt",
            "Ctrl+Shift",
            "Alt+Shift",
            "Ctrl",
            "Alt",
            "Ctrl+Alt+Shift"
        });

        CreateLabel("Клавиша:", 260, hkY, 60, hotkeyGroup);
        _hotkeyKeyComboBox = new ComboBox
        {
            Location = new Point(325, hkY - 3),
            Size = new Size(130, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = hotkeyGroup
        };
        _hotkeyKeyComboBox.Items.AddRange(HotkeyManager.GetAvailableKeys().Cast<object>().ToArray());

        mainPanel.Controls.Add(hotkeyGroup);
        y += 65;

        // === Insertion Settings Group ===
        var insertGroup = CreateGroupBox("Настройки вставки", 10, y, groupWidth, 100);
        var insY = 22;

        CreateLabel("Режим вставки:", 15, insY, 100, insertGroup);
        _insertionModeComboBox = new ComboBox
        {
            Location = new Point(115, insY - 3),
            Size = new Size(200, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = insertGroup
        };
        _insertionModeComboBox.Items.AddRange(new object[]
        {
            "Буфер обмена + Ctrl+V",
            "Эмуляция набора (SendInput)"
        });

        insY += rowHeight + spacing;

        _preserveClipboardCheckBox = new CheckBox
        {
            Text = "Сохранять буфер обмена при вставке",
            Location = new Point(15, insY),
            Size = new Size(280, 23),
            Parent = insertGroup
        };

        insY += rowHeight + spacing;

        _showPreviewOnFocusLostCheckBox = new CheckBox
        {
            Text = "Показывать preview при потере фокуса окна",
            Location = new Point(15, insY),
            Size = new Size(320, 23),
            Parent = insertGroup
        };

        mainPanel.Controls.Add(insertGroup);
        y += 110;

        // === Text Processing Group ===
        var textGroup = CreateGroupBox("Обработка текста", 10, y, groupWidth, 55);
        var textY = 22;

        _punctuationCommandsCheckBox = new CheckBox
        {
            Text = "Включить голосовые команды пунктуации (точка, запятая...)",
            Location = new Point(15, textY),
            Size = new Size(400, 23),
            Parent = textGroup
        };

        mainPanel.Controls.Add(textGroup);
        y += 65;

        // === Storage Settings Group ===
        var storageGroup = CreateGroupBox("Хранение данных", 10, y, groupWidth, 80);
        var storeY = 22;

        _saveAudioCheckBox = new CheckBox
        {
            Text = "Сохранять аудио файлы",
            Location = new Point(15, storeY),
            Size = new Size(200, 23),
            Parent = storageGroup
        };
        _saveAudioCheckBox.CheckedChanged += (s, e) =>
            _audioLogPathTextBox!.Enabled = _saveAudioCheckBox.Checked;

        _audioLogPathTextBox = new TextBox
        {
            Location = new Point(15, storeY + 25),
            Size = new Size(450, 23),
            Enabled = false,
            Parent = storageGroup
        };

        mainPanel.Controls.Add(storageGroup);
        y += 90;

        // === Debug ===
        _debugModeCheckBox = new CheckBox
        {
            Text = "Режим отладки (подробные логи)",
            Location = new Point(15, y),
            Size = new Size(250, 23),
            Parent = mainPanel
        };
        y += 35;

        // === Status ===
        _statusLabel = new Label
        {
            Location = new Point(15, y),
            Size = new Size(280, 20),
            ForeColor = Color.Gray,
            Parent = mainPanel
        };

        // === Buttons ===
        _saveButton = new Button
        {
            Text = "Сохранить",
            Location = new Point(290, y),
            Size = new Size(90, 28),
            Parent = mainPanel
        };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button
        {
            Text = "Отмена",
            Location = new Point(390, y),
            Size = new Size(90, 28),
            Parent = mainPanel
        };
        _cancelButton.Click += (s, e) => Close();

        Controls.Add(mainPanel);
    }

    private GroupBox CreateGroupBox(string title, int x, int y, int width, int height)
    {
        return new GroupBox
        {
            Text = title,
            Location = new Point(x, y),
            Size = new Size(width, height)
        };
    }

    private void CreateLabel(string text, int x, int y, int width, Control parent)
    {
        var label = new Label
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 20),
            Parent = parent
        };
    }

    private void LoadMicrophones()
    {
        var mics = AudioRecorder.GetAvailableMicrophones();
        _microphoneComboBox!.Items.Clear();

        if (mics.Count == 0)
        {
            _microphoneComboBox.Items.Add("Микрофоны не найдены");
            _microphoneComboBox.SelectedIndex = 0;
            return;
        }

        foreach (var mic in mics)
        {
            _microphoneComboBox.Items.Add(mic);
        }

        // Выбираем первый или сохранённый
        if (!string.IsNullOrEmpty(_config.SelectedMicrophoneId))
        {
            var index = mics.FindIndex(m => m.DeviceId == _config.SelectedMicrophoneId);
            if (index >= 0)
            {
                _microphoneComboBox.SelectedIndex = index;
                return;
            }
        }

        _microphoneComboBox.SelectedIndex = 0;
    }

    private void LoadSettings()
    {
        // Local recognition
        _whisperModelTextBox!.Text = _config.WhisperModel;
        _localTimeoutNumeric!.Value = Math.Clamp(_config.LocalWhisperTimeoutSeconds, 60, 1800);
        _hintsTextBox!.Text = _config.HintsDictionary ?? "";

        // Language
        _languageComboBox!.SelectedIndex = _config.Language == RecognitionLanguage.Russian ? 0 : 1;

        // Mode
        _modeComboBox!.SelectedIndex = _config.Mode == RecordingMode.PushToTalk ? 0 : 1;

        // Hotkey
        var modIndex = _hotkeyModifiersComboBox!.Items.IndexOf(_config.HotkeyModifiers);
        _hotkeyModifiersComboBox.SelectedIndex = modIndex >= 0 ? modIndex : 0;

        var keyIndex = _hotkeyKeyComboBox!.Items.IndexOf(_config.HotkeyKey);
        _hotkeyKeyComboBox.SelectedIndex = keyIndex >= 0 ? keyIndex : 0;

        // Recording
        _maxDurationNumeric!.Value = _config.MaxRecordingSeconds;
        _autoStopSilenceCheckBox!.Checked = _config.AutoStopOnSilence;
        _silenceThresholdNumeric!.Value = _config.SilenceThresholdSeconds;
        _silenceThresholdNumeric.Enabled = _config.AutoStopOnSilence;
        _playSoundOnLimitCheckBox!.Checked = _config.PlaySoundOnLimit;
        _enableChunkingCheckBox!.Checked = _config.EnableChunking;
        _chunkSizeNumeric!.Value = _config.ChunkSizeSeconds;
        _chunkSizeNumeric.Enabled = _config.EnableChunking;

        // Insertion
        _insertionModeComboBox!.SelectedIndex = _config.InsertionMode == InsertionMode.Clipboard ? 0 : 1;
        _preserveClipboardCheckBox!.Checked = _config.PreserveClipboard;
        _showPreviewOnFocusLostCheckBox!.Checked = _config.ShowPreviewOnFocusLost;

        // Text
        _punctuationCommandsCheckBox!.Checked = _config.EnablePunctuationCommands;

        // Storage
        _saveAudioCheckBox!.Checked = _config.SaveAudioFiles;
        _audioLogPathTextBox!.Text = _config.AudioLogPath ?? "";
        _audioLogPathTextBox.Enabled = _config.SaveAudioFiles;

        // Debug
        _debugModeCheckBox!.Checked = _config.DebugMode;
    }

    private void SaveSettings()
    {
        // Local recognition
        _config.WhisperModel = "small";
        _config.LocalWhisperTimeoutSeconds = (int)_localTimeoutNumeric!.Value;
        _config.HintsDictionary = _hintsTextBox!.Text.Trim();

        // Language
        _config.Language = _languageComboBox!.SelectedIndex == 0
            ? RecognitionLanguage.Russian
            : RecognitionLanguage.Auto;

        // Mode
        _config.Mode = _modeComboBox!.SelectedIndex == 0
            ? RecordingMode.PushToTalk
            : RecordingMode.Toggle;

        // Hotkey
        _config.HotkeyModifiers = _hotkeyModifiersComboBox!.SelectedItem?.ToString() ?? "Ctrl+Alt";
        _config.HotkeyKey = _hotkeyKeyComboBox!.SelectedItem?.ToString() ?? "Space";

        // Recording
        _config.MaxRecordingSeconds = (int)_maxDurationNumeric!.Value;
        _config.AutoStopOnSilence = _autoStopSilenceCheckBox!.Checked;
        _config.SilenceThresholdSeconds = (int)_silenceThresholdNumeric!.Value;
        _config.PlaySoundOnLimit = _playSoundOnLimitCheckBox!.Checked;
        _config.EnableChunking = _enableChunkingCheckBox!.Checked;
        _config.ChunkSizeSeconds = (int)_chunkSizeNumeric!.Value;

        // Microphone
        if (_microphoneComboBox!.SelectedItem is MicrophoneInfo mic)
        {
            _config.SelectedMicrophoneId = mic.DeviceId;
        }

        // Insertion
        _config.InsertionMode = _insertionModeComboBox!.SelectedIndex == 0
            ? InsertionMode.Clipboard
            : InsertionMode.SendInput;
        _config.PreserveClipboard = _preserveClipboardCheckBox!.Checked;
        _config.ShowPreviewOnFocusLost = _showPreviewOnFocusLostCheckBox!.Checked;

        // Text
        _config.EnablePunctuationCommands = _punctuationCommandsCheckBox!.Checked;

        // Storage
        _config.SaveAudioFiles = _saveAudioCheckBox!.Checked;
        _config.AudioLogPath = _audioLogPathTextBox!.Text;

        // Debug
        _config.DebugMode = _debugModeCheckBox!.Checked;

        // Save
        _configManager.Save();
    }

    private async void TestLocalButton_Click(object? sender, EventArgs e)
    {
        _testLocalButton!.Enabled = false;
        _testLocalButton.Text = "Проверка...";
        _statusLabel!.Text = "Проверка Python и faster-whisper...";
        _statusLabel.ForeColor = Color.Gray;

        try
        {
            var tempConfig = new AppConfig
            {
                WhisperModel = "small"
            };
            using var service = new API.WhisperService(tempConfig);
            var version = await service.CheckAvailabilityAsync();
            _statusLabel.Text = $"Готово: faster-whisper {version}";
            _statusLabel.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Ошибка: {ex.Message}";
            _statusLabel.ForeColor = Color.Red;
        }
        finally
        {
            _testLocalButton.Enabled = true;
            _testLocalButton.Text = "Проверить";
        }
    }

    private void SaveButton_Click(object? sender, EventArgs e)
    {
        SaveSettings();
        MessageBox.Show(
            "Настройки сохранены.\n\nЕсли вы изменили горячую клавишу, она применится после перезапуска.",
            "Настройки",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information
        );
        Close();
    }
}
