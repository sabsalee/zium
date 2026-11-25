using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace Zium.Windows;

internal static class UpdateDistribution
{
    internal const string Repository = "https://github.com/sabsalee/zium";
    internal const string TokenPage = "https://github.com/settings/personal-access-tokens/new";
    internal const string Channel = "win-x64";
    internal const string PackId = "ZiumDesktop";
    internal const string ReleaseApi = "https://api.github.com/repos/sabsalee/zium/releases?per_page=1";

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
    private static readonly HttpClient GitHub = new() { Timeout = TimeSpan.FromSeconds(30) };
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromHours(4) };
    private readonly CancellationTokenSource lifetime = new();
    private readonly UpdateManager installed = UpdateDistribution.Create(new GithubSource(UpdateDistribution.Repository, null, true));
    private string? token;

    internal event Action? Changed;
    internal bool IsInstalled => installed.IsInstalled && !installed.IsPortable;
    internal bool Connected => !string.IsNullOrEmpty(token);
    internal bool Busy { get; private set; }
    internal bool CanRestart { get; private set; }
    internal string Version => installed.CurrentVersion?.ToString() ??
        typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ??
        "0.0.0";
    internal string Message { get; private set; } = "";

    internal void Start()
    {
        token = IsInstalled ? UpdateCredentials.Read() : null;
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
            if (!Connected)
            {
                Publish("자동 업데이트를 시작하려면 GitHub 토큰을 한 번 연결하세요.");
                return;
            }

            Publish("새 버전을 확인하고 있어요.");
            var manager = UpdateDistribution.Create(new GithubSource(UpdateDistribution.Repository, token, true));
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
            Publish(error is HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound }
                ? "GitHub 토큰의 만료일과 Zium 저장소 읽기 권한을 확인해 주세요."
                : "업데이트를 받지 못했어요. 연결을 확인한 뒤 다시 시도해 주세요.");
        }
        finally
        {
            Busy = false;
            Notify();
        }
    }

    internal async Task<bool> ConnectAsync(string value)
    {
        if (!IsInstalled || Busy || lifetime.IsCancellationRequested) return false;
        var candidate = value.Trim();
        if (!candidate.StartsWith("github_pat_", StringComparison.Ordinal) || candidate.Contains(' ') ||
            candidate.Contains('\n') || candidate.Contains('\r'))
        {
            Publish("GitHub에서 만든 Fine-grained 토큰을 입력해 주세요.");
            return false;
        }

        Busy = true;
        try
        {
            Publish("Zium 저장소 읽기 권한을 확인하고 있어요.");
            using var request = new HttpRequestMessage(HttpMethod.Get, UpdateDistribution.ReleaseApi);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", candidate);
            request.Headers.UserAgent.ParseAdd("Zium-Updater/1.0");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await GitHub.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, lifetime.Token);
            if (!response.IsSuccessStatusCode)
            {
                Publish("Zium 저장소에서 토큰을 사용할 수 없어요. 토큰의 저장소 선택과 Contents 읽기 권한을 확인해 주세요.");
                return false;
            }
            UpdateCredentials.Write(candidate);
            token = candidate;
            Publish("GitHub 토큰을 연결했어요.");
            return true;
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { return false; }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or IOException or
            UnauthorizedAccessException or CryptographicException)
        {
            Publish("토큰 연결을 마치지 못했어요. 인터넷과 Windows 사용자 폴더 접근을 확인해 주세요.");
            return false;
        }
        finally
        {
            Busy = false;
            Notify();
        }
    }

    internal void Disconnect()
    {
        if (Busy || !Connected) return;
        try
        {
            UpdateCredentials.Remove();
            token = null;
            if (!ShowPending()) Publish("GitHub 토큰 연결을 해제했어요.");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Publish("저장된 토큰을 지우지 못했어요. Windows 사용자 폴더 접근을 확인해 주세요.");
        }
    }

    internal bool ScheduleOnExit(bool restart)
    {
        try
        {
            if (!IsInstalled || UpdateDistribution.Pending(installed) is not { } pending) return false;
            UpdateDistribution.Schedule(installed, pending, restart);
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
        var pending = UpdateDistribution.Pending(installed);
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
