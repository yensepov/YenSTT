using System.Runtime.InteropServices;
using System.Text;

namespace YenSTT.Core;

/// <summary>
/// Модификаторы горячей клавиши
/// </summary>
[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

/// <summary>
/// Менеджер глобальных горячих клавиш
/// </summary>
public class HotkeyManager : IDisposable
{
    #region Native Methods

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 9000;

    // Virtual Key Codes
    private const int VK_SPACE = 0x20;
    private const int VK_CONTROL = 0x11;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_SHIFT = 0x10;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;

    #endregion

    private readonly IntPtr _windowHandle;
    private HotkeyModifiers _currentModifiers;
    private uint _currentKey;
    private bool _isRegistered;
    private bool _disposed;

    /// <summary>
    /// Событие нажатия горячей клавиши
    /// </summary>
    public event EventHandler<HotkeyEventArgs>? HotkeyPressed;

    /// <summary>
    /// Событие нажатия (KeyDown) - для push-to-talk
    /// </summary>
    public event EventHandler? HotkeyDown;

    /// <summary>
    /// Событие отпускания (KeyUp) - для push-to-talk
    /// </summary>
    public event EventHandler? HotkeyUp;

    /// <summary>
    /// Текущая горячая клавиша заблокирована другим приложением
    /// </summary>
    public event EventHandler<string>? HotkeyConflict;

    /// <summary>
    /// Создать менеджер горячих клавиш
    /// </summary>
    /// <param name="windowHandle">Дескриптор окна для обработки сообщений</param>
    public HotkeyManager(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    /// <summary>
    /// Зарегистрировать горячую клавишу
    /// </summary>
    public bool Register(string modifiers, string key)
    {
        // Сначала отменяем предыдущую регистрацию
        Unregister();

        _currentModifiers = ParseModifiers(modifiers);
        _currentKey = ParseKey(key);

        var result = RegisterHotKey(_windowHandle, HOTKEY_ID, (uint)_currentModifiers, _currentKey);

        if (result)
        {
            _isRegistered = true;
            Utils.Logger.Instance.Info($"Горячая клавиша зарегистрирована: {modifiers}+{key}");
            return true;
        }
        else
        {
            var error = Marshal.GetLastWin32Error();
            var message = error == 1409
                ? "Горячая клавиша уже используется другим приложением"
                : $"Ошибка регистрации горячей клавиши (код: {error})";

            Utils.Logger.Instance.Warning(message);
            HotkeyConflict?.Invoke(this, message);
            return false;
        }
    }

    /// <summary>
    /// Отменить регистрацию горячей клавиши
    /// </summary>
    public void Unregister()
    {
        if (_isRegistered)
        {
            UnregisterHotKey(_windowHandle, HOTKEY_ID);
            _isRegistered = false;
        }
    }

    /// <summary>
    /// Обработать сообщение Windows
    /// </summary>
    public bool ProcessMessage(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
        {
            // Определяем, нажата или отпущена клавиша
            var isPressed = IsKeyPressed(_currentKey);

            HotkeyPressed?.Invoke(this, new HotkeyEventArgs(isPressed));

            if (isPressed)
            {
                HotkeyDown?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                HotkeyUp?.Invoke(this, EventArgs.Empty);
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Проверить, нажата ли клавиша (для push-to-talk режима)
    /// Примечание: Windows Hotkey работает только на KeyUp, поэтому
    /// для push-to-talk нужен другой подход - отслеживание состояния
    /// </summary>
    public bool IsKeyPressed(uint vk)
    {
        var state = GetKeyState((int)vk);
        return (state & 0x8000) != 0;
    }

    /// <summary>
    /// Проверить, нажаты ли модификаторы
    /// </summary>
    public bool AreModifiersPressed()
    {
        bool ctrlPressed = ((ushort)GetKeyState(VK_CONTROL) & 0x8000) != 0;
        bool altPressed = ((ushort)GetKeyState(VK_MENU) & 0x8000) != 0;
        bool shiftPressed = ((ushort)GetKeyState(VK_SHIFT) & 0x8000) != 0;

        bool result = true;

        if (_currentModifiers.HasFlag(HotkeyModifiers.Control) && !ctrlPressed)
            result = false;
        if (_currentModifiers.HasFlag(HotkeyModifiers.Alt) && !altPressed)
            result = false;
        if (_currentModifiers.HasFlag(HotkeyModifiers.Shift) && !shiftPressed)
            result = false;

        return result;
    }

    /// <summary>
    /// Получить заголовок активного окна
    /// </summary>
    public static string GetActiveWindowTitle()
    {
        var handle = GetForegroundWindow();
        var builder = new StringBuilder(256);
        GetWindowText(handle, builder, builder.Capacity);
        return builder.ToString();
    }

    /// <summary>
    /// Разобрать строку модификаторов
    /// </summary>
    private static HotkeyModifiers ParseModifiers(string modifiers)
    {
        var result = HotkeyModifiers.None;
        var parts = modifiers.Split('+', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trim = part.Trim().ToLowerInvariant();
            result |= trim switch
            {
                "ctrl" or "control" => HotkeyModifiers.Control,
                "alt" => HotkeyModifiers.Alt,
                "shift" => HotkeyModifiers.Shift,
                "win" or "windows" => HotkeyModifiers.Win,
                _ => HotkeyModifiers.None
            };
        }

        return result;
    }

    /// <summary>
    /// Разобрать строку клавиши в Virtual Key Code
    /// </summary>
    private static uint ParseKey(string key)
    {
        var upper = key.Trim().ToUpperInvariant();

        return upper switch
        {
            "SPACE" => VK_SPACE,
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F5" => 0x74,
            "F6" => 0x75,
            "F7" => 0x76,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            "INSERT" or "INS" => 0x2D,
            "DELETE" or "DEL" => 0x2E,
            "HOME" => 0x24,
            "END" => 0x23,
            "PAGEUP" => 0x21,
            "PAGEDOWN" => 0x22,
            _ => (uint)upper[0] // Для букв и цифр
        };
    }

    /// <summary>
    /// Получить список доступных клавиш для выбора
    /// </summary>
    public static string[] GetAvailableKeys()
    {
        return new[]
        {
            "Space", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
            "Insert", "Delete", "Home", "End", "PageUp", "PageDown",
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "9"
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Unregister();
            _disposed = true;
        }
    }
}

/// <summary>
/// Аргументы события горячей клавиши
/// </summary>
public class HotkeyEventArgs : EventArgs
{
    public bool IsPressed { get; }

    public HotkeyEventArgs(bool isPressed)
    {
        IsPressed = isPressed;
    }
}
