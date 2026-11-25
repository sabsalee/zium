using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Zium.Core;
using Forms = System.Windows.Forms;

namespace Zium.Windows;

public partial class TodoWindow : Window
{
    private readonly TodoStore store = new(System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zium", "todo.json"));
    private readonly ObservableCollection<TodoRow> rows = [];
    private readonly DragVelocity velocity = new();
    private readonly DispatcherTimer animation = new(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch animationClock = new();
    private DockPosition dock;
    private Forms.NotifyIcon? tray;
    private HwndSource? windowSource;
    private Window? bookmark;
    private TodoRow? editing;
    private (TodoRow Row, int Index)? deleted;
    private ScreenPoint dragPointer, dragOrigin;
    private IntPtr handle;
    private Action<double>? animatedFrame;
    private Action? animationDone;
    private bool moved, quitting, dirty, hotkeyRegistered, ready;

    public TodoWindow()
    {
        InitializeComponent();
        var (state, status) = store.Load();
        foreach (var item in state.Items)
            rows.Add(new(item));
        dock = state.Position;
        Topmost = state.Pinned;
        TaskList.ItemsSource = rows;
        UpdateCount();
        UpdatePin();
        if (status == LoadStatus.Recovered)
            ShowNotice("이전 저장본에서 할 일을 복구했어요.");
        if (status == LoadStatus.Unreadable)
            ShowNotice("저장된 할 일을 읽지 못했어요.");

        animation.Tick += AnimateTick;
        PreviewKeyDown += WindowKeyDown;
        Closing += WindowClosing;
        Closed += WindowClosed;
        DpiChanged += (_, _) => Dispatcher.InvokeAsync(() =>
        {
            if (ready && !animation.IsEnabled && !DragHandle.IsMouseCaptured && IsVisible)
                Reposition();
        }, DispatcherPriority.Loaded);
    }

    internal void Start()
    {
        var interop = new WindowInteropHelper(this);
        handle = interop.EnsureHandle();
        windowSource = HwndSource.FromHwnd(handle);
        windowSource?.AddHook(WindowMessage);
        hotkeyRegistered = NativeWindow.RegisterToggle(handle);
        if (!hotkeyRegistered)
            ShowNotice("Ctrl+Alt+Space를 다른 앱이 사용 중이에요. 트레이에서 열 수 있어요.");

        CreateTray();
        SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        ready = true;
        Reposition();
    }

    private void CreateTray()
    {
        using var stream = Application.GetResourceStream(new Uri("zium.ico", UriKind.Relative)).Stream;
        using var icon = new System.Drawing.Icon(stream);
        tray = new Forms.NotifyIcon
        {
            Icon = (System.Drawing.Icon)icon.Clone(),
            Text = "Zium · 할 일",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip()
        };
        tray.ContextMenuStrip.Items.Add("할 일 열기", null, (_, _) => Reveal(focusEntry: true));
        tray.ContextMenuStrip.Items.Add("가장자리에 접기", null, (_, _) => Tuck());
        tray.ContextMenuStrip.Items.Add("종료", null, (_, _) => Quit());
        tray.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Reveal(focusEntry: true); };
    }

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == NativeWindow.HotkeyMessage && wParam.ToInt32() == NativeWindow.HotkeyId)
        {
            Dispatcher.InvokeAsync(() => { if (dock.Hidden) Reveal(focusEntry: true); else Tuck(); });
            handled = true;
        }
        else if (message == NativeWindow.RestoreMessage)
        {
            Dispatcher.InvokeAsync(() => Reveal(focusEntry: true));
            handled = true;
        }
        return IntPtr.Zero;
    }

    private void DisplaysChanged(object? sender, EventArgs e) => Dispatcher.InvokeAsync(Reposition);

    private void Reposition()
    {
        if (quitting) return;
        StopAnimation();
        var display = Display();
        var work = GetWorkArea(display);
        dock = dock with { Display = display.DeviceName };
        if (dock.Hidden)
        {
            Hide();
            var (size, scale) = PrepareOnDisplay(display, work);
            ShowBookmark(work, size, scale);
            return;
        }
        CloseBookmark();
        var (panel, dpi) = PrepareOnDisplay(display, work);
        var origin = Docking.PanelOrigin(work, panel, dock, dpi);
        if (!IsVisible) ShowAt(origin, null);
        else PlaceFrame(origin, null);
    }

    private (ScreenSize Size, double Scale) PrepareOnDisplay(Forms.Screen display, WorkArea work)
    {
        // Seed a hidden pane on the target display so WPF can obtain that display's scaling before revealing it.
        if (!IsVisible || Forms.Screen.FromHandle(handle).DeviceName != display.DeviceName)
        {
            NativeWindow.Move(handle, new(work.Left + (work.Right - work.Left) / 3,
                work.Top + (work.Bottom - work.Top) / 3));
        }
        var scale = NativeWindow.Scale(handle);
        Width = Math.Min(322, Math.Max(1, (work.Right - work.Left) / scale - 2 * Docking.Margin));
        Height = Math.Min(345, Math.Max(1, (work.Bottom - work.Top) / scale - 2 * Docking.Margin));
        UpdateLayout();
        return (new(Width * scale, Height * scale), scale);
    }

    private void DragStarted(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        StopAnimation();
        PanelFrame.Clip = null;
        dragPointer = NativeWindow.Cursor();
        dragOrigin = NativeWindow.Bounds(handle).Origin;
        velocity.Start(dragPointer, Environment.TickCount64);
        moved = false;
        DragHandle.CaptureMouse();
        e.Handled = true;
    }

    private void DragMoved(object sender, MouseEventArgs e)
    {
        if (!DragHandle.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed) return;
        var pointer = NativeWindow.Cursor();
        var dx = pointer.X - dragPointer.X;
        var dy = pointer.Y - dragPointer.Y;
        velocity.Add(pointer, Environment.TickCount64);
        if (!moved && Math.Max(Math.Abs(dx), Math.Abs(dy)) < 4 * NativeWindow.Scale(handle)) return;
        moved = true;
        NativeWindow.Move(handle, new(dragOrigin.X + dx, dragOrigin.Y + dy));
    }

    private void DragReleased(object sender, MouseButtonEventArgs e)
    {
        if (!DragHandle.IsMouseCaptured) return;
        var pointer = NativeWindow.Cursor();
        FinishDrag(pointer, velocity.Add(pointer, Environment.TickCount64));
        DragHandle.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void DragCaptureLost(object sender, MouseEventArgs e)
    {
        if (moved) FinishDrag(NativeWindow.Cursor(), default);
    }

    private void FinishDrag(ScreenPoint pointer, ScreenPoint speed)
    {
        if (!moved)
        {
            // A new grab can interrupt a settling animation. A click still leaves the pane docked.
            DockAnimation(Display(), dock);
            return;
        }
        moved = false;
        var display = DisplayAt(pointer);
        var (origin, size) = NativeWindow.Bounds(handle);
        var position = Docking.Release(display.DeviceName, GetWorkArea(display), size, origin, pointer,
            speed, NativeWindow.Scale(handle));
        DockAnimation(display, position);
    }

    private void DockAnimation(Forms.Screen display, DockPosition position)
    {
        var changed = dock != position;
        dock = position;
        if (changed) SaveChange();
        var work = GetWorkArea(display);
        var (start, size) = NativeWindow.Bounds(handle);
        var scale = NativeWindow.Scale(handle);
        var target = Docking.PanelOrigin(work, size, dock, scale);
        StartAnimation(start, target, position.Hidden ? 190 : 240, position.Hidden ? work : null, () =>
        {
            if (position.Hidden)
            {
                Hide();
                PanelFrame.Clip = null;
                ShowBookmark(work, size, scale);
            }
            else
                Reposition();
        });
    }

    private void Tuck()
    {
        if (dock.Hidden || quitting) return;
        var display = Display();
        DockAnimation(display, dock with { Display = display.DeviceName, Hidden = true });
    }

    private void Reveal(bool focusEntry)
    {
        if (quitting) return;
        if (!dock.Hidden)
        {
            if (focusEntry) FocusEntry();
            return;
        }

        StopAnimation();
        var display = Display();
        var work = GetWorkArea(display);
        var (panel, scale) = PrepareOnDisplay(display, work);
        var start = Docking.PanelOrigin(work, panel, dock, scale);
        dock = dock with { Hidden = false, Display = display.DeviceName };
        SaveChange();
        var target = Docking.PanelOrigin(work, panel, dock, scale);
        if (!IsVisible) ShowAt(start, work);
        else PlaceFrame(start, work);
        CloseBookmark();
        StartAnimation(start, target, 220, work, () =>
        {
            Reposition();
            if (focusEntry) FocusEntry();
        });
    }

    private void FocusEntry()
    {
        Activate();
        if (editing is null) NewTitle.Focus();
        else EditTitle.Focus();
    }

    private void ShowBookmark(WorkArea work, ScreenSize size, double scale)
    {
        CloseBookmark();
        var isLeft = dock.Side == DockSide.Left;
        var arrow = new System.Windows.Shapes.Path
        {
            Data = Geometry.Parse(isLeft ? "M 3,1 L 8,7 L 3,13" : "M 8,1 L 3,7 L 8,13"),
            StrokeThickness = 1.8,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        arrow.SetResourceReference(Shape.StrokeProperty, "BookmarkTextBrush");
        var button = new Button { Content = arrow, ToolTip = "Zium 할 일 펼치기",
            Style = (Style)FindResource("BookmarkButton") };
        System.Windows.Automation.AutomationProperties.SetName(button, "Zium 할 일 펼치기");
        button.Click += (_, _) => Reveal(focusEntry: true);
        var shape = new Border
        {
            CornerRadius = isLeft ? new CornerRadius(0, 10, 10, 0) : new CornerRadius(10, 0, 0, 10),
            Child = button
        };
        shape.SetResourceReference(Border.BackgroundProperty, "BookmarkBrush");
        bookmark = new Window
        {
            Width = Docking.BookmarkWidth, Height = Docking.BookmarkHeight, WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize, AllowsTransparency = true, Background = Brushes.Transparent,
            ShowInTaskbar = false, ShowActivated = false, Topmost = true, Title = "Zium 책갈피", Content = shape,
            Opacity = 0
        };
        var tabHandle = new WindowInteropHelper(bookmark).EnsureHandle();
        var target = Docking.BookmarkOrigin(work, size, dock, scale);
        NativeWindow.Move(tabHandle, target);
        bookmark.Show();
        var (_, tabSize) = NativeWindow.Bounds(tabHandle);
        NativeWindow.Move(tabHandle, new(isLeft ? work.Left : work.Right - tabSize.Width, target.Y));
        bookmark.Opacity = 1;
    }

    private void CloseBookmark()
    {
        bookmark?.Close();
        bookmark = null;
    }

    private void StartAnimation(ScreenPoint start, ScreenPoint target, int milliseconds, WorkArea? clip, Action done)
    {
        StopAnimation();
        if (!SystemParameters.ClientAreaAnimation || start == target)
        {
            PlaceFrame(target, clip);
            done();
            return;
        }
        animatedFrame = elapsed =>
        {
            var progress = 1 - Math.Pow(1 - Math.Min(elapsed / milliseconds, 1), 3);
            PlaceFrame(new(start.X + (target.X - start.X) * progress, start.Y + (target.Y - start.Y) * progress), clip);
            if (elapsed < milliseconds) return;
            var finish = animationDone;
            StopAnimation();
            finish?.Invoke();
        };
        animationDone = done;
        animationClock.Restart();
        animation.Start();
    }

    private void AnimateTick(object? sender, EventArgs e) => animatedFrame?.Invoke(animationClock.Elapsed.TotalMilliseconds);

    private void StopAnimation()
    {
        animation.Stop();
        animationClock.Stop();
        animatedFrame = null;
        animationDone = null;
    }

    private void ShowAt(ScreenPoint point, WorkArea? clip)
    {
        Opacity = 0;
        PlaceFrame(point, clip);
        Show();
        PlaceFrame(point, clip);
        Opacity = 1;
    }

    private void PlaceFrame(ScreenPoint point, WorkArea? clip)
    {
        NativeWindow.Move(handle, point);
        if (clip is not WorkArea work)
        {
            PanelFrame.Clip = null;
            return;
        }
        var (origin, size) = NativeWindow.Bounds(handle);
        var scale = NativeWindow.Scale(handle);
        var left = Math.Clamp((work.Left - origin.X) / scale, 0, size.Width / scale);
        var top = Math.Clamp((work.Top - origin.Y) / scale, 0, size.Height / scale);
        var right = Math.Clamp((work.Right - origin.X) / scale, left, size.Width / scale);
        var bottom = Math.Clamp((work.Bottom - origin.Y) / scale, top, size.Height / scale);
        PanelFrame.Clip = new RectangleGeometry(new Rect(left, top, right - left, bottom - top));
    }

    private Forms.Screen Display()
    {
        var match = Forms.Screen.AllScreens.FirstOrDefault(display => display.DeviceName == dock.Display);
        return match ?? (string.IsNullOrEmpty(dock.Display) ? DisplayAt(NativeWindow.Cursor()) : Forms.Screen.PrimaryScreen!);
    }

    private static Forms.Screen DisplayAt(ScreenPoint point) =>
        Forms.Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y));

    private static WorkArea GetWorkArea(Forms.Screen display)
    {
        var work = display.WorkingArea;
        return new(work.Left, work.Top, work.Right, work.Bottom);
    }

    private void AddClicked(object sender, RoutedEventArgs e) => AddTask();

    private void NewTitleKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        e.Handled = true;
        AddTask();
    }

    private void AddTask()
    {
        var title = NewTitle.Text.Trim();
        if (title.Length == 0) return;
        rows.Add(new(new(Guid.NewGuid(), title, "", null)));
        NewTitle.Clear();
        NewTitle.Focus();
        TaskScroll.ScrollToEnd();
        SaveChange();
    }

    private void TaskChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox { DataContext: TodoRow row } box) return;
        row.SetCompleted(box.IsChecked == true);
        SaveChange();
    }

    private void TaskHeaderClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TodoRow row }) return;
        if (row.HasNote) row.Update(row.Item, !row.IsExpanded);
        else Edit(row);
    }

    private static TodoRow? MenuRow(object sender) =>
        (sender as MenuItem)?.Parent is ContextMenu { PlacementTarget: FrameworkElement { DataContext: TodoRow row } }
            ? row : null;

    private void EditMenuClicked(object sender, RoutedEventArgs e)
    {
        if (MenuRow(sender) is { } row) Edit(row);
    }

    private void DeleteMenuClicked(object sender, RoutedEventArgs e)
    {
        if (MenuRow(sender) is not { } row) return;
        deleted = (row, rows.IndexOf(row));
        if (editing == row) EndEdit();
        rows.Remove(row);
        if (SaveChange()) ShowNotice("할 일을 삭제했어요. Ctrl+Z로 되돌릴 수 있어요.");
    }

    private void Edit(TodoRow row)
    {
        if (editing is not null)
        {
            EditTitle.Focus();
            return;
        }
        editing = row;
        EditTitle.Text = row.Title;
        EditNote.Text = row.Note;
        QuickEntry.Visibility = Visibility.Collapsed;
        EditFields.Visibility = Visibility.Visible;
        EditTitle.Focus();
        EditTitle.SelectAll();
    }

    private void SaveEditClicked(object sender, RoutedEventArgs e) => SaveEdit();
    private void CancelEditClicked(object sender, RoutedEventArgs e) => EndEdit();

    private void SaveEdit()
    {
        var title = EditTitle.Text.Trim();
        if (editing is null || title.Length == 0)
        {
            EditTitle.Focus();
            return;
        }
        editing.Update(editing.Item with { Title = title, Note = EditNote.Text.Trim() }, true);
        EndEdit();
        SaveChange();
    }

    private void EndEdit()
    {
        editing = null;
        EditFields.Visibility = Visibility.Collapsed;
        QuickEntry.Visibility = Visibility.Visible;
        NewTitle.Focus();
    }

    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (editing is not null) EndEdit();
            else Tuck();
        }
        else if (e.Key == Key.F2 && (e.OriginalSource as FrameworkElement)?.DataContext is TodoRow row)
            Edit(row);
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control && editing is not null)
            SaveEdit();
        else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control &&
            Keyboard.FocusedElement is not TextBox && deleted is { } previous)
        {
            rows.Insert(Math.Min(previous.Index, rows.Count), previous.Row);
            deleted = null;
            if (SaveChange()) ShowNotice("");
        }
        else return;
        e.Handled = true;
    }

    private void PinClicked(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePin();
        SaveChange();
    }

    private void UpdatePin()
    {
        PinButton.SetResourceReference(ForegroundProperty, Topmost ? "AccentBrush" : "MutedBrush");
        if (Topmost) PinButton.SetResourceReference(BackgroundProperty, "HoverBrush");
        else PinButton.Background = Brushes.Transparent;
        var description = Topmost ? "항상 위에 고정됨 · 고정 해제" : "항상 위에 고정";
        PinButton.ToolTip = description;
        System.Windows.Automation.AutomationProperties.SetName(PinButton, description);
    }

    private void HideClicked(object sender, RoutedEventArgs e) => Tuck();

    private bool SaveChange()
    {
        UpdateCount();
        dirty = true;
        try
        {
            store.Save(new(rows.Select(row => row.Item).ToList(), dock, Topmost));
            if (Notice.Text.StartsWith("할 일을 저장하지 못했어요."))
                ShowNotice("");
            dirty = false;
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            ShowNotice("할 일을 저장하지 못했어요. 파일 접근 권한과 디스크 공간을 확인해 주세요.");
            return false;
        }
    }

    private void UpdateCount()
    {
        var remaining = rows.Count(row => !row.IsCompleted);
        TaskCount.Text = rows.Count == 0 ? "" : remaining == 0 ? "모두 완료" : $"{remaining}개 남음";
        EmptyMessage.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowNotice(string text)
    {
        Notice.Text = text;
        Notice.Visibility = text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Quit()
    {
        if (dirty && !SaveChange())
        {
            Reveal(focusEntry: false);
            return;
        }
        quitting = true;
        Application.Current.Shutdown();
    }

    private void WindowClosing(object? sender, CancelEventArgs e)
    {
        if (quitting || Application.Current.Dispatcher.HasShutdownStarted) return;
        e.Cancel = true;
        Tuck();
    }

    private void WindowClosed(object? sender, EventArgs e)
    {
        StopAnimation();
        CloseBookmark();
        SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
        if (hotkeyRegistered) NativeWindow.UnregisterToggle(handle);
        windowSource?.RemoveHook(WindowMessage);
        if (tray is null) return;
        tray.Visible = false;
        tray.Icon?.Dispose();
        tray.ContextMenuStrip?.Dispose();
        tray.Dispose();
    }
}
