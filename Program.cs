using System.Diagnostics;
using System.Runtime.InteropServices;
using YenSTT.UI;

namespace YenSTT;

internal static class Program
{
    #region Native Methods for Single Instance

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_SHOW = 5;

    #endregion

    [STAThread]
    static void Main()
    {
        // Проверяем, что запущен только один экземпляр
        using var mutex = new Mutex(true, "YenSTT_SingleInstance_Mutex", out bool createdNew);

        if (!createdNew)
        {
            // Уже запущен другой экземпляр
            MessageBox.Show(
                "YenSTT уже запущен.\nПроверьте иконку в системном трее.",
                "YenSTT",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
            return;
        }

        // Настраиваем приложение
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Устанавливаем обработчик необработанных исключений
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += Application_ThreadException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

        // Запускаем приложение
        using var app = new TrayApp();
        app.Run();
    }

    /// <summary>
    /// Обработчик необработанных исключений в UI потоке
    /// </summary>
    private static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        Utils.Logger.Instance.Error("Необработанное исключение UI", e.Exception);

        MessageBox.Show(
            $"Произошла ошибка:\n{e.Exception.Message}\n\nПодробности в логе.",
            "Ошибка YenSTT",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error
        );
    }

    /// <summary>
    /// Обработчик необработанных исключений в не-UI потоках
    /// </summary>
    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        Utils.Logger.Instance.Error("Критическое необработанное исключение", exception);

        if (!e.IsTerminating)
        {
            MessageBox.Show(
                $"Произошла критическая ошибка:\n{exception?.Message}\n\nПодробности в логе.",
                "Критическая ошибка YenSTT",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
