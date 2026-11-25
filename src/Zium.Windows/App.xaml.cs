using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Zium.Windows;

public partial class App : Application
{
    private Mutex? instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new Mutex(false, @"Local\Zium.Desktop", out var first);
        if (!first)
        {
            NativeWindow.BroadcastRestore();
            Shutdown();
            return;
        }

        ApplyTheme();
        SystemEvents.UserPreferenceChanged += UserPreferenceChanged;
        var panel = new TodoWindow();
        MainWindow = panel;
        panel.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= UserPreferenceChanged;
        instance?.Dispose();
        base.OnExit(e);
    }

    private void UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        Dispatcher.InvokeAsync(ApplyTheme);

    private void ApplyTheme()
    {
        var light = !Equals(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme", 1), 0);
        SetBrush("PanelBrush", light ? "#FAFBFD" : "#22262D", SystemColors.WindowBrush);
        SetBrush("FieldBrush", light ? "#EEF1F6" : "#191C21", SystemColors.WindowBrush);
        SetBrush("TextBrush", light ? "#202630" : "#F0F2F6", SystemColors.WindowTextBrush);
        SetBrush("MutedBrush", light ? "#566172" : "#A6AFBD", SystemColors.WindowTextBrush);
        SetBrush("DividerBrush", light ? "#DCE2EA" : "#414956", SystemColors.WindowTextBrush);
        SetBrush("HoverBrush", light ? "#E8EDF6" : "#303845", SystemColors.ControlBrush);
        SetBrush("AccentBrush", light ? "#315FCB" : "#9EBFFF", SystemColors.HotTrackBrush);
        SetBrush("BookmarkBrush", light ? "#254C9E" : "#314B7C", SystemColors.HighlightBrush);
        SetBrush("BookmarkTextBrush", "#FFFFFF", SystemColors.HighlightTextBrush);
    }

    private void SetBrush(string key, string hex, Brush highContrast)
    {
        if (SystemParameters.HighContrast)
        {
            Resources[key] = highContrast;
            return;
        }
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        Resources[key] = brush;
    }
}
