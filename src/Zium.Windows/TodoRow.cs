using System;
using System.ComponentModel;
using Zium.Core;

namespace Zium.Windows;

internal sealed class TodoRow(TodoItem item) : INotifyPropertyChanged
{
    public TodoItem Item { get; private set; } = item;
    public string Title => Item.Title;
    public string Note => Item.Note;
    public bool IsCompleted => Item.CompletedAt.HasValue;
    public bool HasNote => Note.Length > 0;
    public bool IsExpanded { get; private set; }
    public string HeaderName => HasNote ? $"{Title} · 메모 {(IsExpanded ? "접기" : "펼치기")}" : $"{Title} · 편집";

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(TodoItem value, bool? expand = null)
    {
        Item = value;
        IsExpanded = HasNote && (expand ?? IsExpanded);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }

    public void SetCompleted(bool completed) =>
        Update(Item with { CompletedAt = completed ? DateTimeOffset.UtcNow : null });
}
