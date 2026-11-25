using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace Zium.Windows;

internal static class UpdateDistribution
{
    internal const string Repository = "https://github.com/sabsalee/zium";
    internal const string Channel = "win-x64";
    internal const string PackId = "ZiumDesktop";

    internal static UpdateManager Create(IUpdateSource source) => new(source,
        new UpdateOptions { ExplicitChannel = Channel, AllowVersionDowngrade = false });

    internal static VelopackAsset? Pending(UpdateManager manager)
    {
        if (!manager.IsInstalled || manager.CurrentVersion is null) return null;
        var pending = manager.UpdatePendingRestart;
        return pending is not null && pending.Version > manager.CurrentVersion ? pending : null;
    }

    internal static void Schedule(UpdateManager manager, VelopackAsset package, bool restart, string[]? args = null) =>
        manager.WaitExitThenApplyUpdates(package, silent: true, restart: restart, restartArgs: args);
}

internal sealed class UpdateService : IDisposable
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromHours(4) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly UpdateManager manager = UpdateDistribution.Create(
        new GithubSource(UpdateDistribution.Repository, accessToken: null, prerelease: true));

    internal event Action? Changed;
    internal bool IsInstalled => manager.IsInstalled && !manager.IsPortable;
    internal bool Busy { get; private set; }
    internal bool CanRestart { get; private set; }
    internal string Version => manager.CurrentVersion?.ToString() ??
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ??
        "0.0.0";
    internal string Message { get; private set; } = "";

    internal void Start()
    {
        timer.Tick += (_, _) => _ = CheckAsync();
        if (IsInstalled) timer.Start();
        _ = CheckAsync();
    }

    internal async Task CheckAsync()
    {
        if (Busy || lifetime.IsCancellationRequested) return;
        if (!IsInstalled)
        {
            Publish("설치 버전에서 자동 업데이트를 사용할 수 있어요.");
            return;
        }
        Busy = true;
        try
        {
            if (ShowPending()) return;
            Publish("새 버전을 확인하고 있어요.");
            var update = await manager.CheckForUpdatesAsync().WaitAsync(TimeSpan.FromSeconds(45), lifetime.Token);
            if (update is null)
            {
                Publish("최신 버전이에요.");
                return;
            }

            var target = update.TargetFullRelease.Version.ToString();
            Publish($"{target} 업데이트를 받고 있어요.");
            await manager.DownloadUpdatesAsync(update, progress: null, lifetime.Token);
            if (!ShowPending()) Publish("업데이트 파일을 확인하지 못했어요. 다시 확인해 주세요.");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
        catch (Exception error)
        {
            Publish(error is TimeoutException
                ? "업데이트 서버 응답이 늦어지고 있어요. 잠시 후 다시 확인해 주세요."
                : "업데이트를 받지 못했어요. 연결을 확인한 뒤 다시 시도해 주세요.");
        }
        finally
        {
            Busy = false;
            Notify();
        }
    }

    internal bool ScheduleOnExit(bool restart)
    {
        try
        {
            if (!IsInstalled || UpdateDistribution.Pending(manager) is not { } pending) return false;
            UpdateDistribution.Schedule(manager, pending, restart);
            return true;
        }
        catch (Exception)
        {
            Publish("업데이트를 시작하지 못했어요. 다시 시도해 주세요.");
            return false;
        }
    }

    private bool ShowPending()
    {
        var pending = IsInstalled ? UpdateDistribution.Pending(manager) : null;
        CanRestart = pending is not null;
        if (pending is null) return false;
        Publish($"{pending.Version} 다운로드 완료. 앱을 종료하면 업데이트돼요.");
        return true;
    }

    private void Publish(string message)
    {
        Message = message;
        Notify();
    }

    private void Notify()
    {
        if (!lifetime.IsCancellationRequested) Changed?.Invoke();
    }

    public void Dispose()
    {
        timer.Stop();
        lifetime.Cancel();
    }
}
