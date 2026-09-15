using Avalonia;
using System;

namespace OperaSuprema;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.ProcessExit += (s, e) => KillLlamaServers();
        AppDomain.CurrentDomain.UnhandledException += (s, e) => KillLlamaServers();
        
        Console.CancelKeyPress += (s, e) => { KillLlamaServers(); e.Cancel = false; };
        System.Runtime.Loader.AssemblyLoadContext.Default.Unloading += (ctx) => KillLlamaServers();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    private static void KillLlamaServers()
    {
        foreach (var proc in System.Diagnostics.Process.GetProcessesByName("llama-server"))
        {
            try { proc.Kill(); } catch { }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
