using System.Text.Json;

namespace Zium.Core;

public sealed record TodoItem(Guid Id, string Title, string Note, DateTimeOffset? CompletedAt);

public sealed record TodoState(List<TodoItem> Items, DockPosition Position, bool Pinned, ScreenSize? WindowSize = null)
{
    public static TodoState Empty => new([], DockPosition.Default, true);
}

public enum LoadStatus { Normal, Recovered, Unreadable }

public sealed class TodoStore(string file)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string backup = file + ".bak";
    private readonly string pending = file + ".tmp";
    private bool preserveBackup;

    public (TodoState State, LoadStatus Status) Load()
    {
        if (TryRead(file, out var state)) return (state, LoadStatus.Normal);
        if (TryRead(backup, out state))
        {
            preserveBackup = true;
            return (state, LoadStatus.Recovered);
        }
        return (TodoState.Empty, File.Exists(file) || File.Exists(backup) ? LoadStatus.Unreadable : LoadStatus.Normal);
    }

    public void Save(TodoState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        try
        {
            using (var stream = new FileStream(pending, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, state, Json);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(file) && !preserveBackup)
                File.Replace(pending, file, backup);
            else
                File.Move(pending, file, overwrite: true);

            preserveBackup = false;
        }
        finally
        {
            if (File.Exists(pending))
                File.Delete(pending);
        }
    }

    private static bool TryRead(string source, out TodoState state)
    {
        try
        {
            using var stream = File.OpenRead(source);
            var saved = JsonSerializer.Deserialize<TodoState>(stream, Json);
            if (saved?.Items is not null && saved.Items.All(item => item is not null && !string.IsNullOrWhiteSpace(item.Title)))
            {
                var position = saved.Position;
                if (!Enum.IsDefined(position.Side) || !double.IsFinite(position.Vertical))
                    position = DockPosition.Default;
                var size = saved.WindowSize;
                if (size is { } value && (!double.IsFinite(value.Width) || !double.IsFinite(value.Height) ||
                    value.Width <= 0 || value.Height <= 0))
                    size = null;
                var normalized = saved.Items.Select(item => item with { Note = item.Note ?? "" }).ToList();
                state = saved with { Items = normalized, Position = position, WindowSize = size };
                return true;
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException) { }

        state = TodoState.Empty;
        return false;
    }
}
