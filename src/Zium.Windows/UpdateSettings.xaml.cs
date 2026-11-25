using System;
using System.Diagnostics;
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
        Closed += (_, _) =>
        {
            service.Changed -= Render;
            TokenInput.Clear();
        };
        Render();
    }

    private void Render()
    {
        using var input = TokenInput.SecurePassword;
        StatusLabel.Text = service.Message;
        Credentials.Visibility = service.IsInstalled ? Visibility.Visible : Visibility.Collapsed;
        ConnectButton.IsEnabled = !service.Busy && input.Length > 0;
        DisconnectButton.Visibility = service.Connected ? Visibility.Visible : Visibility.Collapsed;
        DisconnectButton.IsEnabled = !service.Busy;
        CheckButton.Visibility = service.Connected ? Visibility.Visible : Visibility.Collapsed;
        CheckButton.IsEnabled = !service.Busy && !service.CanRestart;
        RestartButton.Visibility = service.CanRestart ? Visibility.Visible : Visibility.Collapsed;
        RestartButton.IsEnabled = !service.Busy;
    }

    private void TokenChanged(object sender, RoutedEventArgs e) => Render();

    private void OpenTokenPage(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(UpdateDistribution.TokenPage) { UseShellExecute = true }); }
        catch (Exception) { StatusLabel.Text = "브라우저에서 GitHub의 Fine-grained 토큰 설정을 열어 주세요."; }
    }

    private async void ConnectClicked(object sender, RoutedEventArgs e)
    {
        if (!await service.ConnectAsync(TokenInput.Password)) return;
        TokenInput.Clear();
        await service.CheckAsync();
    }

    private void DisconnectClicked(object sender, RoutedEventArgs e) => service.Disconnect();
    private async void CheckClicked(object sender, RoutedEventArgs e) => await service.CheckAsync();
    private void RestartClicked(object sender, RoutedEventArgs e) => restart();
    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
