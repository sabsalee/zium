using System;
using Velopack;

namespace Zium.Windows;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Installer lifecycle events must be handled before WPF or the single-instance mutex starts.
        VelopackApp.Build().SetAutoApplyOnStartup(true).Run();
        if (UpdateInstallationCheck.TryRun(args)) return;

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
