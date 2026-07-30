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
    private ComboBox? _modelComboBox;
    private Label? _modelDetailsLabel;
    private TextBox? _hintsTextBox;

    // Recording Controls
    private ComboBox? _languageComboBox;
    private ComboBox? _modeComboBox;
    private ComboBox? _microphoneComboBox;
    private NumericUpDown? _maxDurationNumeric;
    private CheckBox? _autoStopSilenceCheckBox;
    private NumericUpDown? _silenceThresholdNumeric;
    private CheckBox? _playSoundOnLimitCheckBox;

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
        Size = new Size(620, 815);
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
        const int groupWidth = 580;

        // === Local Recognition Group ===
        var localGroup = CreateGroupBox("Распознавание", 10, y, groupWidth, 150);

        CreateLabel("Модель:", 15, 24, 90, localGroup);
        _modelComboBox = new ComboBox
        {
            Location = new Point(105, 19),
            Size = new Size(270, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = localGroup
        };
        _modelComboBox.Items.AddRange(WhisperModels.All.Cast<object>().ToArray());
        _modelComboBox.SelectedIndexChanged += (_, _) => UpdateModelDetails();

        _modelDetailsLabel = new Label
        {
            Location = new Point(105, 47),
            Size = new Size(450, 38),
            ForeColor = Color.DimGray,
            Parent = localGroup
        };

        var privacyLabel = new Label
        {
            Text = "Распознавание выполняется локально. Аудио не отправляется в интернет.",
            Location = new Point(15, 88),
            Size = new Size(540, 20),
            ForeColor = Color.DimGray,
            Parent = localGroup
        };

        _testLocalButton = new Button
        {
            Text = "Проверить движок",
            Location = new Point(405, 19),
            Size = new Size(150, 28),
            Parent = localGroup
        };
        _testLocalButton.Click += TestLocalButton_Click;

        CreateLabel("Словарь терминов:", 15, 120, 100, localGroup);
        _hintsTextBox = new TextBox
        {
            Location = new Point(115, 117),
            Size = new Size(440, 23),
            PlaceholderText = "Jira, Confluence, API, JSON...",
            Parent = localGroup
        };

        mainPanel.Controls.Add(localGroup);
        y += 160;

        // === Recording Settings Group ===
        var recGroup = CreateGroupBox("Настройки записи", 10, y, groupWidth, 175);
        var recY = 22;

        CreateLabel("Язык:", 15, recY, 100, recGroup);
        _languageComboBox = new ComboBox
        {
            Location = new Point(115, recY - 3),
            Size = new Size(180, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = recGroup
        };
        _languageComboBox.Items.AddRange(new object[] { "Русский", "Автоопределение" });

        recY += rowHeight + spacing;

        CreateLabel("Режим:", 15, recY, 100, recGroup);
        _modeComboBox = new ComboBox
        {
            Location = new Point(115, recY - 3),
            Size = new Size(180, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = recGroup
        };
        _modeComboBox.Items.AddRange(new object[] { "Удерживать клавишу", "Старт/стоп по нажатию" });

        recY += rowHeight + spacing;

        CreateLabel("Микрофон:", 15, recY, 100, recGroup);
        _microphoneComboBox = new ComboBox
        {
            Location = new Point(115, recY - 3),
            Size = new Size(440, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = recGroup
        };
        LoadMicrophones();

        recY += rowHeight + spacing;

        CreateLabel("Лимит (сек):", 15, recY, 100, recGroup);
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
            AutoSize = true,
            Size = new Size(165, 23),
            Parent = recGroup
        };

        _autoStopSilenceCheckBox = new CheckBox
        {
            Text = "Авто-стоп по тишине",
            Location = new Point(370, recY - 3),
            AutoSize = true,
            Size = new Size(180, 23),
            Parent = recGroup
        };
        _autoStopSilenceCheckBox.CheckedChanged += (s, e) =>
            _silenceThresholdNumeric!.Enabled = _autoStopSilenceCheckBox.Checked;

        recY += rowHeight + spacing;

        CreateLabel("Тишина (сек):", 15, recY, 100, recGroup);
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

        mainPanel.Controls.Add(recGroup);
        y += 185;

        // === Hotkey Settings Group ===
        var hotkeyGroup = CreateGroupBox("Горячая клавиша", 10, y, groupWidth, 55);
        var hkY = 22;

        CreateLabel("Сочетание:", 15, hkY, 90, hotkeyGroup);
        _hotkeyModifiersComboBox = new ComboBox
        {
            Location = new Point(105, hkY - 3),
            Size = new Size(155, 23),
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

        CreateLabel("Клавиша:", 280, hkY, 80, hotkeyGroup);
        _hotkeyKeyComboBox = new ComboBox
        {
            Location = new Point(360, hkY - 3),
            Size = new Size(195, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = hotkeyGroup
        };
        _hotkeyKeyComboBox.Items.AddRange(HotkeyManager.GetAvailableKeys().Cast<object>().ToArray());

        mainPanel.Controls.Add(hotkeyGroup);
        y += 65;

        // === Insertion Settings Group ===
        var insertGroup = CreateGroupBox("Настройки вставки", 10, y, groupWidth, 115);
        var insY = 22;

        CreateLabel("Режим вставки:", 15, insY, 100, insertGroup);
        _insertionModeComboBox = new ComboBox
        {
            Location = new Point(115, insY - 3),
            Size = new Size(300, 23),
            DropDownStyle = ComboBoxStyle.DropDownList,
            Parent = insertGroup
        };
        _insertionModeComboBox.Items.AddRange(new object[]
        {
            "Через буфер обмена (рекомендуется)",
            "Посимвольный ввод (совместимость)"
        });

        insY += rowHeight + spacing;

        _preserveClipboardCheckBox = new CheckBox
        {
            Text = "Сохранять буфер обмена при вставке",
            Location = new Point(15, insY),
            AutoSize = true,
            Size = new Size(520, 23),
            Parent = insertGroup
        };

        insY += rowHeight + spacing;

        _showPreviewOnFocusLostCheckBox = new CheckBox
        {
            Text = "Показывать предпросмотр, если окно потеряло фокус",
            Location = new Point(15, insY),
            AutoSize = true,
            Size = new Size(520, 23),
            Parent = insertGroup
        };

        mainPanel.Controls.Add(insertGroup);
        y += 125;

        // === Text Processing Group ===
        var textGroup = CreateGroupBox("Обработка текста", 10, y, groupWidth, 55);
        var textY = 22;

        _punctuationCommandsCheckBox = new CheckBox
        {
            Text = "Включить голосовые команды пунктуации (точка, запятая...)",
            Location = new Point(15, textY),
            AutoSize = true,
            Size = new Size(540, 23),
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
            AutoSize = true,
            Size = new Size(200, 23),
            Parent = storageGroup
        };
        _saveAudioCheckBox.CheckedChanged += (s, e) =>
            _audioLogPathTextBox!.Enabled = _saveAudioCheckBox.Checked;

        _audioLogPathTextBox = new TextBox
        {
            Location = new Point(15, storeY + 25),
            Size = new Size(550, 23),
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
            AutoSize = true,
            Size = new Size(350, 23),
            Parent = mainPanel
        };
        y += 35;

        // === Status ===
        _statusLabel = new Label
        {
            Location = new Point(15, y),
            Size = new Size(375, 20),
            ForeColor = Color.Gray,
            Parent = mainPanel
        };

        // === Buttons ===
        _saveButton = new Button
        {
            Text = "Сохранить",
            Location = new Point(375, y),
            Size = new Size(100, 28),
            Parent = mainPanel
        };
        _saveButton.Click += SaveButton_Click;

        _cancelButton = new Button
        {
            Text = "Отмена",
            Location = new Point(485, y),
            Size = new Size(100, 28),
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
        var model = WhisperModels.Get(_config.WhisperModel);
        _modelComboBox!.SelectedItem = WhisperModels.All.First(item => item.Name == model.Name);
        UpdateModelDetails();
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
        _config.WhisperModel = (_modelComboBox!.SelectedItem as WhisperModelInfo)?.Name
            ?? WhisperModels.DefaultName;
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
        _statusLabel!.Text = "Проверка встроенного движка...";
        _statusLabel.ForeColor = Color.Gray;

        try
        {
            var tempConfig = new AppConfig
            {
                WhisperModel = (_modelComboBox!.SelectedItem as WhisperModelInfo)?.Name
                    ?? WhisperModels.DefaultName
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
            _testLocalButton.Text = "Проверить движок";
        }
    }

    private void UpdateModelDetails()
    {
        if (_modelDetailsLabel == null)
            return;

        var model = _modelComboBox?.SelectedItem as WhisperModelInfo
            ?? WhisperModels.Get(WhisperModels.DefaultName);
        _modelDetailsLabel.Text = $"{model.DownloadSize} · {model.Details} · CPU int8";
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
