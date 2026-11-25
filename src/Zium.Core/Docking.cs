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
    public const double ShadowInset = 12;
    public const double FrameGap = Margin - ShadowInset;
    public const double BookmarkWidth = 27;
    public const double BookmarkHeight = 86;
    public static ScreenSize DefaultWindowSize => new(346, 369);
    public static ScreenSize MinimumWindowSize => new(270, 225);

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
        var side = Math.Abs(velocity.X) >= ThrowSpeed * scale
            ? (velocity.X < 0 ? DockSide.Left : DockSide.Right)
            : (origin.X + panel.Width / 2 <= center ? DockSide.Left : DockSide.Right);
        var distance = side == DockSide.Left ? pointer.X - work.Left : work.Right - pointer.X;
        var hidden = horizontal && Math.Abs(velocity.X) >= HideSpeed * scale && distance <= HideZone * scale;
        var projectedY = origin.Y + panel.Height / 2 + Math.Clamp(velocity.Y * ProjectionSeconds, -120 * scale, 120 * scale);
        var towardBottom = Math.Abs(velocity.Y) >= ThrowSpeed * scale
            ? velocity.Y > 0
            : projectedY > (work.Top + work.Bottom) / 2;
        var vertical = towardBottom ? 1 : 0;
        return new(display, side, vertical, hidden);
    }

    public static ScreenPoint PanelOrigin(WorkArea work, ScreenSize panel, DockPosition position, double scale)
    {
        var edge = position.Side == DockSide.Left ? work.Left : work.Right;
        var (top, bottom) = VerticalRange(work, panel.Height, scale);
        var left = position.Hidden
            ? (position.Side == DockSide.Left ? edge - panel.Width : edge)
            : (position.Side == DockSide.Left ? edge + FrameGap * scale : edge - panel.Width - FrameGap * scale);
        return new(left, position.Vertical < .5 ? top : bottom);
    }

    public static ScreenPoint BookmarkOrigin(WorkArea work, ScreenSize bookmark, DockPosition position, double scale)
    {
        var (top, bottom) = VerticalRange(work, bookmark.Height, scale);
        var left = position.Side == DockSide.Left ? work.Left : work.Right - bookmark.Width;
        return new(left, position.Vertical < .5 ? top : bottom);
    }

    public static ScreenSize MaximumWindowSize(WorkArea work, double scale) => new(
        Math.Max(1, (work.Right - work.Left) / scale - 2 * FrameGap),
        Math.Max(1, (work.Bottom - work.Top) / scale - 2 * FrameGap));

    public static ScreenSize ClampWindowSize(ScreenSize size, ScreenSize maximum) => new(
        Math.Clamp(size.Width, Math.Min(MinimumWindowSize.Width, maximum.Width), maximum.Width),
        Math.Clamp(size.Height, Math.Min(MinimumWindowSize.Height, maximum.Height), maximum.Height));

    private static (double Top, double Bottom) VerticalRange(WorkArea work, double height, double scale)
    {
        var margin = FrameGap * scale;
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
