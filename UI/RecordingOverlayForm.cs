using System.Drawing.Drawing2D;

namespace YenSTT.UI;

/// <summary>
/// Компактное оверлей-окно записи (не крадёт фокус у целевого окна)
/// </summary>
public class RecordingOverlayForm : Form
{
    public event EventHandler? StopRequested;

    private Panel _recordDot = null!;
    private Label _statusLabel = null!;
    private Label _timerLabel = null!;
    private Button _stopButton = null!;
    private System.Windows.Forms.Timer _animationTimer = null!;
    private System.Windows.Forms.Timer _durationTimer = null!;
    private DateTime _startTime;
    private bool _dotVisible = true;

    // Не перехватываем фокус у целевого окна
    protected override bool ShowWithoutActivation => true;

    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WM_MOUSEACTIVATE = 0x0021;
    private const int MA_NOACTIVATE = 3;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            return cp;
        }
    }

    protected override void WndProc(ref Message m)
    {
        // Клик по оверлею не крадёт фокус
        if (m.Msg == WM_MOUSEACTIVATE)
        {
            m.Result = (IntPtr)MA_NOACTIVATE;
            return;
        }
        base.WndProc(ref m);
    }

    public RecordingOverlayForm()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        // --- Form ---
        Text = string.Empty;
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true;
        ShowInTaskbar = false;
        Size = new Size(280, 48);
        BackColor = Color.FromArgb(45, 45, 45);

        // Позиция: внизу по центру экрана
        var screen = Screen.PrimaryScreen!.WorkingArea;
        Location = new Point(
            screen.X + (screen.Width - Width) / 2,
            screen.Y + screen.Height - Height - 16
        );

        // Скруглённые углы
        SetRoundedRegion(12);

        // --- Красная точка записи ---
        _recordDot = new Panel
        {
            Size = new Size(14, 14),
            Location = new Point(14, 17),
            BackColor = Color.Red
        };
        using (var dotPath = new GraphicsPath())
        {
            dotPath.AddEllipse(0, 0, 14, 14);
            _recordDot.Region = new Region(dotPath);
        }

        // --- Статус ---
        _statusLabel = new Label
        {
            Text = "Запись...",
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9.5f),
            Location = new Point(34, 13),
            AutoSize = true
        };

        // --- Таймер ---
        _timerLabel = new Label
        {
            Text = "00:00",
            ForeColor = Color.FromArgb(160, 160, 160),
            BackColor = Color.Transparent,
            Font = new Font("Segoe UI", 9f),
            Location = new Point(150, 14),
            AutoSize = true
        };

        // --- Кнопка Стоп ---
        _stopButton = new Button
        {
            Size = new Size(36, 30),
            Location = new Point(232, 9),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(70, 70, 70),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand,
            TabStop = false,
            Text = "\u25A0" // ■
        };
        _stopButton.FlatAppearance.BorderSize = 0;
        _stopButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(100, 100, 100);
        _stopButton.Click += (s, e) => StopRequested?.Invoke(this, EventArgs.Empty);

        Controls.AddRange(new Control[] { _recordDot, _statusLabel, _timerLabel, _stopButton });

        // --- Таймеры ---
        _animationTimer = new System.Windows.Forms.Timer { Interval = 500 };
        _animationTimer.Tick += (s, e) =>
        {
            _dotVisible = !_dotVisible;
            _recordDot.Visible = _dotVisible;
        };

        _durationTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _durationTimer.Tick += (s, e) =>
        {
            var elapsed = DateTime.Now - _startTime;
            _timerLabel.Text = elapsed.ToString(@"mm\:ss");
        };
    }

    private void SetRoundedRegion(int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(0, 0, d, d, 180, 90);
        path.AddArc(Width - d, 0, d, d, 270, 90);
        path.AddArc(Width - d, Height - d, d, d, 0, 90);
        path.AddArc(0, Height - d, d, d, 90, 90);
        path.CloseFigure();
        Region = new Region(path);
    }

    /// <summary>Показать состояние «Запись»</summary>
    public void ShowRecording()
    {
        _startTime = DateTime.Now;
        _recordDot.BackColor = Color.Red;
        _recordDot.Visible = true;
        _dotVisible = true;
        _statusLabel.Text = "Запись...";
        _timerLabel.Text = "00:00";
        _timerLabel.Visible = true;
        _stopButton.Visible = true;
        _animationTimer.Start();
        _durationTimer.Start();
        TopMost = true;
        Show();
    }

    /// <summary>Показать состояние «Обработка»</summary>
    public void ShowProcessing(string? message = null)
    {
        _animationTimer.Stop();
        _durationTimer.Stop();
        _recordDot.BackColor = Color.Orange;
        _recordDot.Visible = true;
        _statusLabel.Text = message ?? "Распознавание...";
        _timerLabel.Visible = false;
        _stopButton.Visible = false;
    }

    /// <summary>Показать «Вставлено!»</summary>
    public void ShowSuccess()
    {
        _animationTimer.Stop();
        _durationTimer.Stop();
        _recordDot.BackColor = Color.LimeGreen;
        _recordDot.Visible = true;
        _statusLabel.Text = "Вставлено!";
        _timerLabel.Visible = false;
        _stopButton.Visible = false;
    }

    /// <summary>Показать ошибку</summary>
    public void ShowError(string message)
    {
        _animationTimer.Stop();
        _durationTimer.Stop();
        _recordDot.BackColor = Color.OrangeRed;
        _recordDot.Visible = true;
        _statusLabel.Text = message;
        _timerLabel.Visible = false;
        _stopButton.Visible = false;
    }

    /// <summary>Скрыть оверлей</summary>
    public void HideOverlay()
    {
        _animationTimer.Stop();
        _durationTimer.Stop();
        Hide();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animationTimer?.Dispose();
            _durationTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
