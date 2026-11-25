using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using Velopack.Sources;
using Zium.Core;

namespace Zium.Windows;

internal static class UpdateInstallationCheck
{
    private const string TestTitle = "설치 전 할 일";
    private const string TestNote = "업데이트 뒤에도 남는 메모";

    internal static bool TryRun(string[] args)
    {
        if (Environment.GetEnvironmentVariable("ZIUM_INSTALLER_CHECKS") != "1" || args.Length != 5 ||
            args[0] is not ("--check-installed-update" or "--check-installed-restart"))
            return false;

        var report = args[2];
        try
        {
            var data = Path.GetDirectoryName(report)!;
            var store = new TodoStore(Path.Combine(data, "todo.json"));
            var update = UpdateDistribution.Create(new SimpleFileSource(new DirectoryInfo(args[1])));
            var version = update.CurrentVersion?.ToString();
            var baselineMarker = Path.Combine(AppContext.BaseDirectory, "baseline-only.marker");
            if (!update.IsInstalled || update.IsPortable || update.AppId != UpdateDistribution.PackId)
                throw new InvalidOperationException("Zium did not start from an installer-managed directory.");

            if (args[0] == "--check-installed-update")
            {
                if (version != args[3] || !File.Exists(baselineMarker))
                    throw new InvalidOperationException("The baseline package was not installed.");
                store.Save(TodoState.Empty with { Items = [new(Guid.NewGuid(), TestTitle, TestNote, null)] });
                var available = update.CheckForUpdatesAsync().WaitAsync(TimeSpan.FromSeconds(45)).GetAwaiter().GetResult();
                if (available?.TargetFullRelease.Version.ToString() != args[4])
                    throw new InvalidOperationException("The new prerelease was not offered.");
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                update.DownloadUpdatesAsync(available!, null, timeout.Token).GetAwaiter().GetResult();
                var pending = UpdateDistribution.Pending(update);
                if (pending?.Version.ToString() != args[4])
                    throw new InvalidOperationException("The verified update was not ready.");
                UpdateDistribution.Schedule(update, pending!, restart: true, ["--check-installed-restart", .. args.Skip(1)]);
            }
            else
            {
                if (version != args[4] || File.Exists(baselineMarker))
                    throw new InvalidOperationException("The installer did not replace the old version.");
                var saved = store.Load().State.Items.Single();
                if (saved.Title != TestTitle || saved.Note != TestNote)
                    throw new InvalidOperationException("Local data could not be read after updating.");
                Report(report, new { status = "passed", version });
            }
        }
        catch (Exception error)
        {
            Report(report, new { status = "failed", error = error.ToString() });
            Environment.ExitCode = 1;
        }
        return true;
    }

    private static void Report(string path, object result)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(result));
        File.Move(temp, path, overwrite: true);
    }
}
