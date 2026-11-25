using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Zium.Core;

namespace Zium.Windows;

public partial class TodoWindow
{
    private bool resizing;
    private ScreenPoint resizePointer, resizeOrigin;
    private ScreenSize resizeSize;
    private double resizeScale;

    private void ResizeStarted(object sender, DragStartedEventArgs e)
    {
        StopAnimation();
        WindowSurface.Clip = null;
        CloseBookmark();
        dock = dock with { Hidden = false };
        resizePointer = NativeWindow.Cursor();
        resizeOrigin = NativeWindow.Bounds(handle).Origin;
        resizeSize = new(ActualWidth, ActualHeight);
        resizeScale = NativeWindow.Scale(handle);
        resizing = true;
    }

    private void ResizeMoved(object sender, DragDeltaEventArgs e)
    {
        if (!resizing || sender is not Thumb { Tag: string edge }) return;
        var pointer = NativeWindow.Cursor();
        var dx = (pointer.X - resizePointer.X) / resizeScale;
        var dy = (pointer.Y - resizePointer.Y) / resizeScale;
        Width = Math.Clamp(resizeSize.Width + (edge.Contains('W') ? -dx : edge.Contains('E') ? dx : 0), MinWidth, MaxWidth);
        Height = Math.Clamp(resizeSize.Height + (edge.Contains('N') ? -dy : edge.Contains('S') ? dy : 0), MinHeight, MaxHeight);
        NativeWindow.Move(handle, new(
            resizeOrigin.X + (edge.Contains('W') ? (resizeSize.Width - Width) * resizeScale : 0),
            resizeOrigin.Y + (edge.Contains('N') ? (resizeSize.Height - Height) * resizeScale : 0)));
    }

    private void ResizeCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!resizing) return;
        resizing = false;
        if (e.Canceled) { Reposition(); return; }
        preferredSize = new(Width, Height);
        UpdateLayout();
        DockAnimation(Display(), dock);
        SaveChange();
    }

    private void UpdateResizeMark()
    {
        var left = dock.Side == DockSide.Right;
        var top = dock.Vertical >= .5;
        ResizeMark.HorizontalAlignment = left ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        ResizeMark.VerticalAlignment = top ? VerticalAlignment.Top : VerticalAlignment.Bottom;
        ResizeMark.RenderTransform = new ScaleTransform(left ? -1 : 1, top ? -1 : 1);
    }
}
