using System;
using System.IO;
using Avalonia;

namespace AdministracionSenderistas;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            try
            {
                File.WriteAllText("crash.log", $"[{DateTime.Now}] Unhandled Exception:\n{ex}");
            }
            catch { }
        };

        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText("crash.log", $"[{DateTime.Now}] Fatal App Exception:\n{ex}");
            }
            catch { }
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
