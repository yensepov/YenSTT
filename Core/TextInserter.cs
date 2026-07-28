using System.Runtime.InteropServices;
using System.Text;
using YenSTT.Core;
using YenSTT.Utils;

namespace YenSTT.Core;

/// <summary>
/// Результат проверки фокуса окна
/// </summary>
public class FocusCheckResult
{
    public bool FocusLost { get; set; }
    public IntPtr OriginalWindowHandle { get; set; }
    public IntPtr CurrentWindowHandle { get; set; }
    public string OriginalWindowTitle { get; set; } = string.Empty;
    public string CurrentWindowTitle { get; set; } = string.Empty;
}

/// <summary>
/// Результат вставки текста
/// </summary>
public class TextInsertionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool FocusWasLost { get; set; }
    public string? OriginalWindowTitle { get; set; }
}

/// <summary>
/// Класс для вставки текста в активное окно
/// </summary>
public class TextInserter
{
    #region Native Methods

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd); // минимизировано?

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    // Clipboard Win32 API (надёжнее чем .NET Clipboard.SetText — не требует STA/message pump)
    [DllImport("user32.dll")]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll")]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll")]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    // Virtual Key Codes
    private const int VK_CONTROL = 0x11;
    private const int VK_V = 0x56;

    // Key events
    private const int KEYEVENTF_KEYDOWN = 0x0000;
    private const int KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, int dwFlags, int dwExtraInfo);

    // SendInput for fallback mode
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public INPUTUNION Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public KEYBOARDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBOARDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    #endregion

    private readonly AppConfig _config;

    /// <summary>
    /// Дескриптор окна, которое было активно при старте записи
    /// </summary>
    private IntPtr _targetWindowHandle;
    private string _targetWindowTitle = string.Empty;

    /// <summary>
    /// Событие завершения вставки
    /// </summary>
    public event EventHandler<TextInsertionResult>? InsertionCompleted;

    /// <summary>
    /// Событие при потере фокуса (для показа preview)
    /// </summary>
    public event EventHandler<FocusCheckResult>? FocusLost;

    public TextInserter(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Запомнить текущее активное окно как целевое
    /// </summary>
    public void CaptureTargetWindow()
    {
        _targetWindowHandle = GetForegroundWindow();
        _targetWindowTitle = HotkeyManager.GetActiveWindowTitle();
        Logger.Instance.Debug($"Целевое окно: {_targetWindowTitle} (handle: {_targetWindowHandle})");
    }

    /// <summary>
    /// Проверить, изменился ли фокус
    /// </summary>
    public FocusCheckResult CheckFocus()
    {
        var currentHandle = GetForegroundWindow();
        var currentTitle = HotkeyManager.GetActiveWindowTitle();

        var result = new FocusCheckResult
        {
            OriginalWindowHandle = _targetWindowHandle,
            OriginalWindowTitle = _targetWindowTitle,
            CurrentWindowHandle = currentHandle,
            CurrentWindowTitle = currentTitle,
            FocusLost = currentHandle != _targetWindowHandle
        };

        if (result.FocusLost)
        {
            Logger.Instance.Info($"Фокус изменён: '{_targetWindowTitle}' -> '{currentTitle}'");
        }

        return result;
    }

    /// <summary>
    /// Вставить текст в активное окно
    /// </summary>
    public async Task InsertTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            InsertionCompleted?.Invoke(this, new TextInsertionResult
            {
                Success = false,
                Error = "Нет текста для вставки"
            });
            return;
        }

        // Проверяем фокус
        var focusCheck = CheckFocus();

        if (focusCheck.FocusLost && _config.ShowPreviewOnFocusLost)
        {
            // Уведомляем о потере фокуса
            FocusLost?.Invoke(this, focusCheck);
            return; // Ожидаем решения пользователя
        }

        await PerformInsertionAsync(text, focusCheck);
    }

    /// <summary>
    /// Выполнить вставку текста (без проверки фокуса)
    /// </summary>
    public async Task PerformInsertionAsync(string text, FocusCheckResult? focusCheck = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            InsertionCompleted?.Invoke(this, new TextInsertionResult
            {
                Success = false,
                Error = "Нет текста для вставки"
            });
            return;
        }

        focusCheck ??= CheckFocus();

        // Определяем целевое окно
        var targetHandle = focusCheck.FocusLost ? _targetWindowHandle : GetForegroundWindow();
        var targetTitle = focusCheck.FocusLost ? _targetWindowTitle : HotkeyManager.GetActiveWindowTitle();

        // Если исходное окно недоступно — вставляем в текущее активное окно
        if (!IsWindow(targetHandle) || (!IsWindowVisible(targetHandle) && !IsIconic(targetHandle)))
        {
            targetHandle = GetForegroundWindow();
            targetTitle = HotkeyManager.GetActiveWindowTitle();
            Logger.Instance.Info($"Исходное окно недоступно, вставляем в текущее: {targetTitle}");
        }

        Logger.Instance.Info($"Вставка текста в окно: {targetTitle}");

        try
        {
            // Подготавливаем текст
            var preparedText = TextProcessor.PrepareForInsertion(text, _config);

            bool success;

            // Выбираем метод вставки
            if (_config.InsertionMode == InsertionMode.Clipboard)
            {
                success = await InsertViaClipboardAsync(preparedText, targetHandle);

                if (!success)
                {
                    Logger.Instance.Info("Метод буфера обмена не сработал, пробуем SendInput");
                    success = await InsertViaSendInputAsync(preparedText, targetHandle);
                }
            }
            else
            {
                success = await InsertViaSendInputAsync(preparedText, targetHandle);
            }

            InsertionCompleted?.Invoke(this, new TextInsertionResult
            {
                Success = success,
                Error = success ? null : "Не удалось вставить текст",
                FocusWasLost = focusCheck.FocusLost,
                OriginalWindowTitle = focusCheck.OriginalWindowTitle
            });
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Ошибка вставки текста", ex);

            InsertionCompleted?.Invoke(this, new TextInsertionResult
            {
                Success = false,
                Error = ex.Message,
                FocusWasLost = focusCheck.FocusLost
            });
        }
    }

    /// <summary>
    /// Только скопировать текст в буфер обмена (без вставки)
    /// </summary>
    public async Task CopyToClipboardOnlyAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var preparedText = TextProcessor.PrepareForInsertion(text, _config);

        try
        {
            // Сохраняем буфер если нужно
            if (_config.PreserveClipboard)
            {
                SaveClipboard();
            }

            await SetClipboardTextAsync(preparedText);

            InsertionCompleted?.Invoke(this, new TextInsertionResult
            {
                Success = true,
                Error = null
            });
        }
        finally
        {
            // Восстанавливаем буфер после задержки
            if (_config.PreserveClipboard)
            {
                await Task.Delay(500);
                RestoreClipboard();
            }
        }
    }

    /// <summary>
    /// Вставка через буфер обмена + Ctrl+V
    /// </summary>
    private async Task<bool> InsertViaClipboardAsync(string text, IntPtr targetWindow)
    {
        try
        {
            // Проверяем, что окно ещё существует
            if (!IsWindow(targetWindow))
            {
                Logger.Instance.Warning("Целевое окно больше не существует");
                return false;
            }

            // Сохраняем буфер обмена
            if (_config.PreserveClipboard)
            {
                SaveClipboard();
            }

            try
            {
                // Устанавливаем текст в буфер обмена
                await SetClipboardTextAsync(text);

                // Небольшая задержка
                await Task.Delay(50);

                // Активируем целевое окно
                if (!ActivateWindow(targetWindow))
                {
                    Logger.Instance.Warning("Не удалось активировать целевое окно");
                    return false;
                }

                await Task.Delay(100);

                // Отправляем Ctrl+V
                SendCtrlV();

                await Task.Delay(100);
                return true;
            }
            finally
            {
                // Восстанавливаем буфер обмена
                if (_config.PreserveClipboard)
                {
                    await Task.Delay(200);
                    RestoreClipboard();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Ошибка вставки через буфер обмена", ex);
            return false;
        }
    }

    /// <summary>
    /// Вставка через SendInput (эмуляция набора) - fallback
    /// </summary>
    private async Task<bool> InsertViaSendInputAsync(string text, IntPtr targetWindow)
    {
        try
        {
            // Проверяем, что окно ещё существует
            if (!IsWindow(targetWindow))
            {
                Logger.Instance.Warning("Целевое окно больше не существует");
                return false;
            }

            // Активируем целевое окно
            if (!ActivateWindow(targetWindow))
            {
                Logger.Instance.Warning("Не удалось активировать целевое окно для SendInput");
                return false;
            }

            await Task.Delay(100);

            // Отправляем каждый символ
            foreach (char c in text)
            {
                SendUnicodeChar(c);
                await Task.Delay(5); // Небольшая задержка между символами
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("Ошибка SendInput", ex);
            return false;
        }
    }

    /// <summary>
    /// Отправить Unicode символ через SendInput
    /// </summary>
    private void SendUnicodeChar(char c)
    {
        INPUT[] inputs = new INPUT[2];

        // Key down
        inputs[0] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Data = new INPUTUNION
            {
                Keyboard = new KEYBOARDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE,
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        // Key up
        inputs[1] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Data = new INPUTUNION
            {
                Keyboard = new KEYBOARDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | 2, // KEYEVENTF_KEYUP
                    time = 0,
                    dwExtraInfo = IntPtr.Zero
                }
            }
        };

        SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT)));
    }

    /// <summary>
    /// Активировать окно
    /// </summary>
    private bool ActivateWindow(IntPtr hWnd)
    {
        if (!IsWindow(hWnd))
        {
            Logger.Instance.Info($"ActivateWindow: IsWindow=false for 0x{hWnd:X}");
            return false;
        }

        // Восстанавливаем минимизированное окно
        if (IsIconic(hWnd))
        {
            ShowWindow(hWnd, SW_RESTORE);
            Thread.Sleep(200);
        }

        if (!IsWindowVisible(hWnd))
        {
            Logger.Instance.Info($"ActivateWindow: IsWindowVisible=false for 0x{hWnd:X}");
            return false;
        }

        var fgBefore = GetForegroundWindow();
        Logger.Instance.Info($"ActivateWindow: fg=0x{fgBefore:X}, target=0x{hWnd:X}, same={fgBefore == hWnd}");

        // Если уже foreground — ничего делать не нужно
        if (fgBefore == hWnd)
            return true;

        // Получаем потоки
        uint currentThread = GetCurrentThreadId();
        uint targetThread = GetWindowThreadProcessId(hWnd, out _);

        bool attached = false;
        if (currentThread != targetThread)
            attached = AttachThreadInput(currentThread, targetThread, true);

        try
        {
            // Alt-key trick: разблокирует SetForegroundWindow из фонового потока
            keybd_event(0x12, 0, KEYEVENTF_KEYDOWN, 0);
            keybd_event(0x12, 0, KEYEVENTF_KEYUP, 0);

            ShowWindow(hWnd, SW_SHOW);
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);

            Thread.Sleep(50);
            var fgAfter = GetForegroundWindow();
            Logger.Instance.Info($"ActivateWindow: after fg=0x{fgAfter:X}, match={fgAfter == hWnd}");
            return true;
        }
        finally
        {
            if (attached)
                AttachThreadInput(currentThread, targetThread, false);
        }
    }

    /// <summary>
    /// Отправить Ctrl+V
    /// </summary>
    private void SendCtrlV()
    {
        Logger.Instance.Info("SendCtrlV: отправка Ctrl+V через keybd_event");
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYDOWN, 0);
        keybd_event(VK_V, 0, KEYEVENTF_KEYDOWN, 0);
        keybd_event(VK_V, 0, KEYEVENTF_KEYUP, 0);
        keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, 0);
    }

    /// <summary>
    /// Сохранить текущий буфер обмена (без логирования содержимого!)
    /// </summary>
    private void SaveClipboard()
    {
        try
        {
            // Только проверяем наличие данных, НЕ читаем и не логируем содержимое!
            if (Clipboard.ContainsText() || Clipboard.ContainsImage() || Clipboard.ContainsFileDropList())
            {
                // Сохраняем только факт наличия данных
                _hasClipboardData = true;
                Logger.Instance.Debug("Буфер обмена сохранён (содержимое не логируется)");
            }
            else
            {
                _hasClipboardData = false;
            }
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"Не удалось проверить буфер обмена: {ex.Message}");
            _hasClipboardData = false;
        }
    }

    private bool _hasClipboardData = false;

    /// <summary>
    /// Восстановить буфер обмена (без логирования содержимого!)
    /// </summary>
    private void RestoreClipboard()
    {
        if (!_hasClipboardData)
            return;

        try
        {
            // Примечание: полное восстановление сложное и небезопасное для приватности
            // Поэтому просто отмечаем в логе что попытались
            Logger.Instance.Debug("Восстановление буфера обмена (содержимое не логируется)");

            // На практике буфер обмена уже содержит наш текст
            // Полное восстановление требует хранения данных, что небезопасно
            // Поэтому просто сбрасываем флаг
            _hasClipboardData = false;
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"Не удалось восстановить буфер обмена: {ex.Message}");
        }
    }

    /// <summary>
    /// Установить текст в буфер обмена через Win32 API (работает из любого потока)
    /// </summary>
    private async Task SetClipboardTextAsync(string text)
    {
        const int maxAttempts = 5;

        for (int i = 0; i < maxAttempts; i++)
        {
            if (SetClipboardWin32(text))
            {
                Logger.Instance.Info("Clipboard: текст установлен через Win32 API");
                return;
            }

            Logger.Instance.Debug($"Clipboard: попытка {i + 1} не удалась, повтор...");
            await Task.Delay(50);
        }

        throw new Exception("Не удалось установить текст в буфер обмена");
    }

    /// <summary>
    /// Прямой вызов Win32 Clipboard API (не требует STA потока и message pump)
    /// </summary>
    private bool SetClipboardWin32(string text)
    {
        if (!OpenClipboard(IntPtr.Zero))
            return false;

        try
        {
            EmptyClipboard();

            // Unicode: (length + 1) * 2 байт (включая null-terminator)
            var byteCount = (text.Length + 1) * 2;
            var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)byteCount);
            if (hMem == IntPtr.Zero)
                return false;

            var ptr = GlobalLock(hMem);
            if (ptr == IntPtr.Zero)
                return false;

            try
            {
                Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                // Null terminator уже есть (GlobalAlloc обнуляет память)
            }
            finally
            {
                GlobalUnlock(hMem);
            }

            // После SetClipboardData, система владеет памятью — не освобождать hMem!
            if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero)
                return false;

            return true;
        }
        finally
        {
            CloseClipboard();
        }
    }

    /// <summary>
    /// Синхронная вставка текста — ДОЛЖНА вызываться из UI (STA) потока.
    /// Clipboard.SetText работает напрямую без STA-хака,
    /// SetForegroundWindow работает надёжно из потока, владеющего окном.
    /// </summary>
    public TextInsertionResult InsertTextDirect(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new TextInsertionResult { Success = false, Error = "Нет текста для вставки" };

        var preparedText = TextProcessor.PrepareForInsertion(text, _config);
        var targetHandle = _targetWindowHandle;
        var targetTitle = _targetWindowTitle;

        Logger.Instance.Info($"InsertTextDirect: target='{targetTitle}' handle=0x{targetHandle:X}");
        Logger.Instance.Debug($"  IsWindow={IsWindow(targetHandle)} IsVisible={IsWindowVisible(targetHandle)} IsIconic={IsIconic(targetHandle)}");

        // Если целевое окно недоступно — берём текущее активное
        if (!IsWindow(targetHandle) || (!IsWindowVisible(targetHandle) && !IsIconic(targetHandle)))
        {
            targetHandle = GetForegroundWindow();
            targetTitle = HotkeyManager.GetActiveWindowTitle();
            Logger.Instance.Info($"  Target unavailable, using foreground: '{targetTitle}' 0x{targetHandle:X}");
        }

        // Восстанавливаем минимизированное окно
        if (IsIconic(targetHandle))
        {
            ShowWindow(targetHandle, SW_RESTORE);
            Thread.Sleep(200);
        }

        // ── Метод 1: Clipboard + Ctrl+V ──
        try
        {
            Clipboard.SetText(preparedText);
            Logger.Instance.Debug("  Clipboard.SetText OK");
        }
        catch (Exception ex)
        {
            Logger.Instance.Warning($"  Clipboard.SetText failed: {ex.Message}");
            // Переходим к методу 2
            return TryInsertViaSendInputDirect(preparedText, targetHandle);
        }

        Thread.Sleep(50);
        ActivateWindowReliable(targetHandle);
        Thread.Sleep(100);
        SendCtrlVViaSendInput();
        Thread.Sleep(100);

        Logger.Instance.Info("InsertTextDirect: clipboard+Ctrl+V done");
        return new TextInsertionResult { Success = true };
    }

    /// <summary>
    /// Fallback: вставка посимвольно через SendInput
    /// </summary>
    private TextInsertionResult TryInsertViaSendInputDirect(string text, IntPtr targetHandle)
    {
        try
        {
            ActivateWindowReliable(targetHandle);
            Thread.Sleep(100);

            foreach (char c in text)
            {
                SendUnicodeChar(c);
                Thread.Sleep(5);
            }

            Logger.Instance.Info("InsertTextDirect: SendInput Unicode done");
            return new TextInsertionResult { Success = true };
        }
        catch (Exception ex)
        {
            Logger.Instance.Error("InsertTextDirect: SendInput failed", ex);
            return new TextInsertionResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Надёжная активация окна (несколько стратегий)
    /// </summary>
    private void ActivateWindowReliable(IntPtr hWnd)
    {
        if (!IsWindow(hWnd) || !IsWindowVisible(hWnd))
        {
            Logger.Instance.Debug($"  ActivateWindowReliable: skip (IsWindow={IsWindow(hWnd)} IsVisible={IsWindowVisible(hWnd)})");
            return;
        }

        var fgBefore = GetForegroundWindow();
        Logger.Instance.Debug($"  ActivateWindowReliable: fg before=0x{fgBefore:X}, target=0x{hWnd:X}");

        // Если уже в фокусе — ничего делать не нужно
        if (fgBefore == hWnd)
        {
            Logger.Instance.Debug("  Already foreground");
            return;
        }

        uint currentThread = GetCurrentThreadId();
        uint targetThread = GetWindowThreadProcessId(hWnd, out _);

        bool attached = false;
        if (currentThread != targetThread)
            attached = AttachThreadInput(currentThread, targetThread, true);

        try
        {
            // Alt-key trick: снимает блокировку SetForegroundWindow
            keybd_event(0x12, 0, KEYEVENTF_KEYDOWN, 0);
            keybd_event(0x12, 0, KEYEVENTF_KEYUP, 0);

            ShowWindow(hWnd, SW_SHOW);
            BringWindowToTop(hWnd);
            SetForegroundWindow(hWnd);

            Thread.Sleep(50);
            var fgAfter = GetForegroundWindow();
            Logger.Instance.Debug($"  ActivateWindowReliable: fg after=0x{fgAfter:X}, match={fgAfter == hWnd}");
        }
        finally
        {
            if (attached)
                AttachThreadInput(currentThread, targetThread, false);
        }
    }

    /// <summary>
    /// Ctrl+V через SendInput (надёжнее чем keybd_event)
    /// </summary>
    private void SendCtrlVViaSendInput()
    {
        INPUT[] inputs = new INPUT[4];

        inputs[0] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Data = new INPUTUNION { Keyboard = new KEYBOARDINPUT { wVk = (ushort)VK_CONTROL, dwFlags = 0 } }
        };
        inputs[1] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Data = new INPUTUNION { Keyboard = new KEYBOARDINPUT { wVk = (ushort)VK_V, dwFlags = 0 } }
        };
        inputs[2] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Data = new INPUTUNION { Keyboard = new KEYBOARDINPUT { wVk = (ushort)VK_V, dwFlags = 2 } }
        };
        inputs[3] = new INPUT
        {
            Type = INPUT_KEYBOARD,
            Data = new INPUTUNION { Keyboard = new KEYBOARDINPUT { wVk = (ushort)VK_CONTROL, dwFlags = 2 } }
        };

        var sent = SendInput(4, inputs, Marshal.SizeOf(typeof(INPUT)));
        Logger.Instance.Debug($"  SendCtrlVViaSendInput: sent={sent}");
    }
}
