using Avalonia;
using System;
using System.Runtime.InteropServices;

namespace OperaSuprema;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) => KillLlamaServers();
        AppDomain.CurrentDomain.UnhandledException += (s, e) => KillLlamaServers();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += (ctx) => KillLlamaServers();
            Console.CancelKeyPress += (s, e) => { KillLlamaServers(); e.Cancel = false; };
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void KillLlamaServers()
    {
        foreach (var proc in System.Diagnostics.Process.GetProcessesByName("llama-server"))
        {
            try { proc.Kill(); } catch { }
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
