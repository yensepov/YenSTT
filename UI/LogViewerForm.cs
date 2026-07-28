namespace YenSTT.UI;

/// <summary>
/// Форма просмотра логов
/// </summary>
public class LogViewerForm : Form
{
    private TextBox? _logTextBox;
    private ComboBox? _logFileComboBox;
    private Button? _refreshButton;
    private Button? _openFolderButton;
    private Button? _closeButton;

    public LogViewerForm()
    {
        InitializeComponents();
        LoadLogFiles();
    }

    private void InitializeComponents()
    {
        Text = "Логи YenSTT";
        Size = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;

        // Top panel
        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(10, 5, 10, 5)
        };

        var fileLabel = new Label
        {
            Text = "Файл:",
            Location = new Point(10, 10),
            Size = new Size(50, 20)
        };

        _logFileComboBox = new ComboBox
        {
            Location = new Point(60, 8),
            Size = new Size(200, 23),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _logFileComboBox.SelectedIndexChanged += (s, e) => LoadSelectedLog();

        _refreshButton = new Button
        {
            Text = "Обновить",
            Location = new Point(270, 6),
            Size = new Size(90, 26)
        };
        _refreshButton.Click += (s, e) => LoadSelectedLog();

        _openFolderButton = new Button
        {
            Text = "Открыть папку",
            Location = new Point(370, 6),
            Size = new Size(110, 26)
        };
        _openFolderButton.Click += OpenFolderButton_Click;

        topPanel.Controls.AddRange(new Control[]
        {
            fileLabel,
            _logFileComboBox,
            _refreshButton,
            _openFolderButton
        });

        // Log text box
        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 9),
            ReadOnly = true,
            WordWrap = false
        };

        // Bottom panel
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            Padding = new Padding(10, 5, 10, 5)
        };

        _closeButton = new Button
        {
            Text = "Закрыть",
            Anchor = AnchorStyles.Right,
            Size = new Size(90, 28)
        };
        _closeButton.Location = new Point(bottomPanel.Width - 100, 8);
        _closeButton.Anchor = AnchorStyles.Right;
        _closeButton.Click += (s, e) => Close();

        bottomPanel.Controls.Add(_closeButton);

        Controls.AddRange(new Control[] { _logTextBox, topPanel, bottomPanel });
    }

    private void LoadLogFiles()
    {
        var logDir = Core.ConfigManager.GetLogDirectory();

        if (!Directory.Exists(logDir))
        {
            _logFileComboBox!.Items.Add("Логов пока нет");
            _logFileComboBox.SelectedIndex = 0;
            return;
        }

        var files = Directory.GetFiles(logDir, "log_*.txt")
            .OrderByDescending(f => f)
            .ToList();

        _logFileComboBox!.Items.Clear();

        if (files.Count == 0)
        {
            _logFileComboBox.Items.Add("Логов пока нет");
            _logFileComboBox.SelectedIndex = 0;
            return;
        }

        foreach (var file in files)
        {
            _logFileComboBox.Items.Add(Path.GetFileName(file));
        }

        _logFileComboBox.SelectedIndex = 0;
    }

    private void LoadSelectedLog()
    {
        var selected = _logFileComboBox?.SelectedItem?.ToString();

        if (string.IsNullOrEmpty(selected) || selected == "Логов пока нет")
        {
            _logTextBox!.Text = "Нет записей лога";
            return;
        }

        var logDir = Core.ConfigManager.GetLogDirectory();
        var filePath = Path.Combine(logDir, selected);

        try
        {
            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                _logTextBox!.Text = content;

                // Scroll to end
                _logTextBox.SelectionStart = _logTextBox.Text.Length;
                _logTextBox.ScrollToCaret();
            }
            else
            {
                _logTextBox!.Text = "Файл не найден";
            }
        }
        catch (Exception ex)
        {
            _logTextBox!.Text = $"Ошибка чтения файла: {ex.Message}";
        }
    }

    private void OpenFolderButton_Click(object? sender, EventArgs e)
    {
        var logDir = Core.ConfigManager.GetLogDirectory();

        if (!Directory.Exists(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        System.Diagnostics.Process.Start("explorer.exe", logDir);
    }
}
