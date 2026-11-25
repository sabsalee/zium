using System;
using System.Windows;

namespace Zium.Windows;

public partial class UpdateSettings : Window
{
    private readonly UpdateService service;
    private readonly Action restart;

    internal UpdateSettings(UpdateService service, Action restart)
    {
        this.service = service;
        this.restart = restart;
        InitializeComponent();
        VersionLabel.Text = service.Version;
        service.Changed += Render;
        Closed += (_, _) => service.Changed -= Render;
        Render();
    }

    private void Render()
    {
        StatusLabel.Text = service.Message;
        CheckButton.Visibility = service.IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        CheckButton.IsEnabled = !service.Busy && !service.CanRestart;
        RestartButton.Visibility = service.CanRestart ? Visibility.Visible : Visibility.Collapsed;
        RestartButton.IsEnabled = !service.Busy;
    }

    private async void CheckClicked(object sender, RoutedEventArgs e) => await service.CheckAsync();
    private void RestartClicked(object sender, RoutedEventArgs e) => restart();
    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
