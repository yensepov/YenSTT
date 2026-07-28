using YenSTT.Core;

namespace YenSTT.UI;

/// <summary>
/// Результат выбора пользователя в Preview форме
/// </summary>
public enum PreviewResult
{
    /// <summary>
    /// Вставить текст в исходное окно
    /// </summary>
    Insert,

    /// <summary>
    /// Скопировать в буфер обмена
    /// </summary>
    Copy,

    /// <summary>
    /// Отмена
    /// </summary>
    Cancel
}

/// <summary>
/// Окно предпросмотра текста при потере фокуса
/// </summary>
public class PreviewForm : Form
{
    private readonly string _text;
    private readonly FocusCheckResult _focusCheck;
    private readonly TextInserter _textInserter;

    private TextBox? _previewTextBox;
    private Label? _infoLabel;
    private Button? _insertButton;
    private Button? _copyButton;
    private Button? _cancelButton;

    private PreviewResult _result = PreviewResult.Cancel;

    /// <summary>
    /// Результат выбора пользователя
    /// </summary>
    public PreviewResult Result => _result;

    /// <summary>
    /// Отредактированный текст
    /// </summary>
    public string EditedText => _previewTextBox?.Text ?? string.Empty;

    public PreviewForm(string text, FocusCheckResult focusCheck, TextInserter textInserter)
    {
        _text = text;
        _focusCheck = focusCheck;
        _textInserter = textInserter;

        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // Form settings
        Text = "YenSTT - Предпросмотр";
        Size = new Size(500, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true; // Поверх всех окон

        // Info label
        var origTitle = string.IsNullOrEmpty(_focusCheck.OriginalWindowTitle) ? "(нет)" : _focusCheck.OriginalWindowTitle;
        var currTitle = string.IsNullOrEmpty(_focusCheck.CurrentWindowTitle) ? "(нет)" : _focusCheck.CurrentWindowTitle;
        _infoLabel = new Label
        {
            Location = new Point(15, 12),
            Size = new Size(460, 56),
            Text = $"⚠ Фокус изменился!\n" +
                   $"Исходное окно: {origTitle}\n" +
                   $"Текущее окно:  {currTitle}",
            ForeColor = Color.DarkOrange,
            Font = new Font("Segoe UI", 9)
        };

        // Preview text box
        _previewTextBox = new TextBox
        {
            Location = new Point(15, 74),
            Size = new Size(455, 160),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 10),
            Text = _text
        };

        // Buttons panel
        var buttonY = 225;
        const int buttonWidth = 120;
        const int buttonHeight = 30;
        const int spacing = 15;

        _insertButton = new Button
        {
            Text = "↩ Вставить",
            Location = new Point(15, buttonY),
            Size = new Size(buttonWidth, buttonHeight),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _insertButton.Click += InsertButton_Click;

        _copyButton = new Button
        {
            Text = "📋 Копировать",
            Location = new Point(15 + buttonWidth + spacing, buttonY),
            Size = new Size(buttonWidth, buttonHeight)
        };
        _copyButton.Click += CopyButton_Click;

        _cancelButton = new Button
        {
            Text = "Отмена",
            Location = new Point(15 + (buttonWidth + spacing) * 2, buttonY),
            Size = new Size(buttonWidth, buttonHeight)
        };
        _cancelButton.Click += (s, e) =>
        {
            _result = PreviewResult.Cancel;
            Close();
        };

        // Help text
        var helpLabel = new Label
        {
            Location = new Point(15, 260),
            Size = new Size(455, 20),
            Text = "Текст можно отредактировать перед вставкой или копированием.",
            ForeColor = Color.Gray,
            Font = new Font("Segoe UI", 8)
        };

        Controls.AddRange(new Control[]
        {
            _infoLabel,
            _previewTextBox,
            _insertButton,
            _copyButton,
            _cancelButton,
            helpLabel
        });

        // Accept/Cancel buttons
        AcceptButton = _insertButton;
        CancelButton = _cancelButton;

        // Focus on text box
        ActiveControl = _previewTextBox;
        _previewTextBox.SelectionStart = _previewTextBox.Text.Length;
    }

    private void InsertButton_Click(object? sender, EventArgs e)
    {
        _result = PreviewResult.Insert;
        Close();
    }

    private void CopyButton_Click(object? sender, EventArgs e)
    {
        _result = PreviewResult.Copy;

        // Копируем в буфер обмена
        try
        {
            Clipboard.SetText(EditedText);
            Utils.Logger.Instance.Info("Текст скопирован в буфер обмена");
        }
        catch (Exception ex)
        {
            Utils.Logger.Instance.Error("Ошибка копирования в буфер", ex);
        }

        Close();
    }

    /// <summary>
    /// Показать форму и получить результат
    /// </summary>
    public static PreviewResult ShowDialog(string text, FocusCheckResult focusCheck, TextInserter textInserter, out string editedText)
    {
        using var form = new PreviewForm(text, focusCheck, textInserter);
        form.ShowDialog();
        editedText = form.EditedText;
        return form.Result;
    }
}
