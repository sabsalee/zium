using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Zium.Windows;

public partial class TodoWindow
{
    private TodoRow? orderCandidate, draggingRow;
    private Point orderStart;
    private Border? orderLine;
    private long orderLastScroll;

    private void OrderPressed(object sender, MouseButtonEventArgs e)
    {
        orderCandidate = (sender as Button)?.Tag as TodoRow;
        orderStart = e.GetPosition(TaskList);
    }

    private void OrderMoved(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed ||
            sender is not Button { IsPressed: true, Tag: TodoRow row } button || row != orderCandidate)
            return;
        var delta = e.GetPosition(TaskList) - orderStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        orderCandidate = null;
        button.ReleaseMouseCapture();
        draggingRow = row;
        orderLastScroll = 0;
        try
        {
            DragDrop.DoDragDrop(button, new DataObject("Zium.TaskOrder", row.Item.Id.ToString()), DragDropEffects.Move);
        }
        finally
        {
            draggingRow = null;
            SetOrderLine(null);
        }
    }

    private void OrderDragOver(object sender, DragEventArgs e)
    {
        if (draggingRow is null || sender is not Border { DataContext: TodoRow target } row) return;
        e.Handled = true;
        var after = e.GetPosition(row).Y >= row.ActualHeight / 2;
        e.Effects = draggingRow == target ? DragDropEffects.None : DragDropEffects.Move;
        SetOrderLine(draggingRow == target ? null : row, after);
    }

    private void OrderDragLeave(object sender, DragEventArgs e)
    {
        if (sender is not Border { Child: Grid content } || orderLine?.Parent != content) return;
        var point = e.GetPosition(content);
        if (point.X < 0 || point.Y < 0 || point.X >= content.ActualWidth || point.Y >= content.ActualHeight)
            SetOrderLine(null);
    }

    private void OrderDrop(object sender, DragEventArgs e)
    {
        if (draggingRow is null || sender is not Border { DataContext: TodoRow target } row) return;
        e.Handled = true;
        var targetIndex = rows.IndexOf(target);
        if (targetIndex < 0) return;
        MoveTask(targetIndex + (e.GetPosition(row).Y >= row.ActualHeight / 2 ? 1 : 0));
    }

    private void OrderScrollDragOver(object sender, DragEventArgs e)
    {
        if (draggingRow is null) return;
        e.Effects = DragDropEffects.Move;
        var now = Environment.TickCount64;
        if (now - orderLastScroll < 70) return;
        var y = e.GetPosition(TaskScroll).Y;
        if (y < 24) TaskScroll.LineUp();
        else if (y > TaskScroll.ActualHeight - 24) TaskScroll.LineDown();
        else return;
        orderLastScroll = now;
    }

    private void OrderScrollDrop(object sender, DragEventArgs e)
    {
        if (draggingRow is null) return;
        e.Handled = true;
        MoveTask(rows.Count);
    }

    private void SetOrderLine(Border? target, bool after = false)
    {
        var next = (target?.Child as Grid)?.Children.OfType<Border>().FirstOrDefault();
        if (orderLine != next)
        {
            orderLine?.ClearValue(Border.BorderThicknessProperty);
            orderLine = next;
        }
        if (orderLine is not null)
            orderLine.BorderThickness = after ? new Thickness(0, 0, 0, 2) : new Thickness(0, 2, 0, 0);
    }

    private void MoveTask(int insertion)
    {
        if (draggingRow is not { } row) return;
        var from = rows.IndexOf(row);
        if (from < 0) return;
        var to = insertion > from ? insertion - 1 : insertion;
        if (from == to) return;
        rows.Move(from, to);
        SaveChange();
    }
}
