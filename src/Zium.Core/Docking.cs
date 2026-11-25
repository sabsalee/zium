namespace Zium.Core;

public enum DockSide { Left, Right }

public readonly record struct ScreenPoint(double X, double Y);
public readonly record struct ScreenSize(double Width, double Height);
public readonly record struct WorkArea(double Left, double Top, double Right, double Bottom);
public readonly record struct DockPosition(string Display, DockSide Side, double Vertical, bool Hidden)
{
    public static DockPosition Default => new("", DockSide.Right, 0, false);
}

public static class Docking
{
    public const double Margin = 16;
    public const double BookmarkWidth = 28;
    public const double BookmarkHeight = 56;

    private const double ThrowSpeed = 550;
    private const double HideSpeed = 1050;
    private const double HideZone = 112;
    private const double DirectionRatio = 1.35;
    private const double ProjectionSeconds = .16;

    public static DockPosition Release(string display, WorkArea work, ScreenSize panel, ScreenPoint origin,
        ScreenPoint pointer, ScreenPoint velocity, double scale)
    {
        var center = (work.Left + work.Right) / 2;
        var horizontal = Math.Abs(velocity.X) >= Math.Abs(velocity.Y) * DirectionRatio;
        var thrown = horizontal && Math.Abs(velocity.X) >= ThrowSpeed * scale;
        var side = thrown
            ? (velocity.X < 0 ? DockSide.Left : DockSide.Right)
            : (origin.X + panel.Width / 2 <= center ? DockSide.Left : DockSide.Right);
        var distance = side == DockSide.Left ? pointer.X - work.Left : work.Right - pointer.X;
        var hidden = thrown && Math.Abs(velocity.X) >= HideSpeed * scale && distance <= HideZone * scale;
        var projectedY = origin.Y + Math.Clamp(velocity.Y * ProjectionSeconds, -120 * scale, 120 * scale);
        var (top, bottom) = VerticalRange(work, panel.Height, scale);
        var vertical = bottom == top ? 0 : Math.Clamp((projectedY - top) / (bottom - top), 0, 1);
        return new(display, side, vertical, hidden);
    }

    public static ScreenPoint PanelOrigin(WorkArea work, ScreenSize panel, DockPosition position, double scale)
    {
        var edge = position.Side == DockSide.Left ? work.Left : work.Right;
        var (top, bottom) = VerticalRange(work, panel.Height, scale);
        var left = position.Hidden
            ? (position.Side == DockSide.Left ? edge - panel.Width : edge)
            : (position.Side == DockSide.Left ? edge + Margin * scale : edge - panel.Width - Margin * scale);
        return new(left, top + (bottom - top) * Math.Clamp(position.Vertical, 0, 1));
    }

    public static ScreenPoint BookmarkOrigin(WorkArea work, ScreenSize panel, DockPosition position, double scale)
    {
        var panelOrigin = PanelOrigin(work, panel, position, scale);
        var (top, bottom) = VerticalRange(work, BookmarkHeight * scale, scale);
        var left = position.Side == DockSide.Left ? work.Left : work.Right - BookmarkWidth * scale;
        return new(left, Math.Clamp(panelOrigin.Y + (panel.Height - BookmarkHeight * scale) / 2, top, bottom));
    }

    private static (double Top, double Bottom) VerticalRange(WorkArea work, double height, double scale)
    {
        var margin = Margin * scale;
        var top = work.Top + Math.Min(margin, Math.Max(0, (work.Bottom - work.Top - height) / 2));
        return (top, Math.Max(top, work.Bottom - margin - height));
    }
}

public sealed class DragVelocity
{
    private const long SampleWindow = 120;
    private readonly Queue<(ScreenPoint Position, long Time)> samples = new();

    public void Start(ScreenPoint position, long milliseconds)
    {
        samples.Clear();
        Add(position, milliseconds);
    }

    public ScreenPoint Add(ScreenPoint position, long milliseconds)
    {
        samples.Enqueue((position, milliseconds));
        while (milliseconds - samples.Peek().Time > SampleWindow)
            samples.Dequeue();

        var first = samples.Peek();
        var elapsed = milliseconds - first.Time;
        return elapsed < 16 ? default : new(
            (position.X - first.Position.X) * 1000 / elapsed,
            (position.Y - first.Position.Y) * 1000 / elapsed);
    }
}
