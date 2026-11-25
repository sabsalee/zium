using Zium.Core;

var work = new WorkArea(0, 0, 1920, 1040);
var panel = Docking.DefaultWindowSize;
var bookmark = new ScreenSize(Docking.BookmarkWidth + Docking.ShadowInset,
    Docking.BookmarkHeight + 2 * Docking.ShadowInset);

var slow = Docking.Release("main", work, panel, new(1500, 1080), new(1760, 1020), new(100, 60), 1);
Expect(slow.Side == DockSide.Right && !slow.Hidden, "A slow drag docks to the nearest side");
Expect(Docking.PanelOrigin(work, panel, slow, 1) == new ScreenPoint(1570, 667),
    "Docking keeps the panel off the right and bottom edges");

var throughMiddle = Docking.Release("main", work, panel, new(400, 190), new(650, 220), new(1450, 10), 1);
Expect(throughMiddle.Side == DockSide.Right && !throughMiddle.Hidden, "A rightward throw across the work area docks right");

var offRight = Docking.Release("main", work, panel, new(1760, 190), new(1895, 220), new(1450, 10), 1);
Expect(offRight.Side == DockSide.Right && offRight.Hidden, "A fast outward throw near the right edge hides");
Expect(Docking.PanelOrigin(work, panel, offRight, 1).X == work.Right, "The pane leaves the work area");
Expect(Docking.BookmarkOrigin(work, bookmark, offRight, 1) == new ScreenPoint(1881, 4),
    "The bookmark remains at the same corner inside the work area");

var inward = Docking.Release("main", work, panel, new(1640, 190), new(1860, 220), new(-1400, 0), 1);
Expect(!inward.Hidden && inward.Side == DockSide.Left, "An inward throw near the right edge cannot hide on the right");
var vertical = Docking.Release("main", work, panel, new(1660, 190), new(1860, 220), new(1300, -1450), 1);
Expect(!vertical.Hidden, "A diagonal primarily vertical move cannot hide the pane");
var parkedNearEdge = Docking.Release("main", work, panel, new(1700, 190), new(1900, 220), default, 1);
Expect(!parkedNearEdge.Hidden, "Releasing after a pause near the edge still docks");

var corners = new[]
{
    (new ScreenPoint(-740, -740), new ScreenPoint(4, 4)),
    (new ScreenPoint(-740, 740), new ScreenPoint(4, 667)),
    (new ScreenPoint(740, -740), new ScreenPoint(1570, 4)),
    (new ScreenPoint(740, 740), new ScreenPoint(1570, 667))
};
foreach (var (speed, expected) in corners)
{
    var released = Docking.Release("main", work, panel, new(800, 340), new(960, 520), speed, 1);
    Expect(!released.Hidden && Docking.PanelOrigin(work, panel, released, 1) == expected,
        $"A diagonal throw settles in the intended corner for {speed}");
}

var bigger = new ScreenSize(600, 620);
var bottom = new DockPosition("main", DockSide.Right, 1, false);
Expect(Docking.PanelOrigin(work, bigger, bottom, 1) == new ScreenPoint(1316, 416),
    "A larger pane preserves the right and bottom margins");
Expect(Docking.PanelOrigin(work, panel, bottom with { Vertical = .8 }, 1).Y == 667,
    "A saved vertical position from preview 1 maps to the nearest corner");
var maximum = Docking.MaximumWindowSize(new(0, 0, 560, 480), 2);
Expect(Docking.ClampWindowSize(bigger, maximum) == maximum, "A saved window fits a smaller high-DPI display");

var secondary = new WorkArea(-2560, -120, -45, 1280);
var largePanel = new ScreenSize(panel.Width * 1.5, panel.Height * 1.5);
var offLeft = Docking.Release("secondary", secondary, largePanel, new(-2570, -180), new(-2500, -70), new(-1700, 0), 1.5);
Expect(offLeft.Hidden && offLeft.Side == DockSide.Left, "Left hiding uses the scaled edge of the monitor under the mouse");
Expect(Docking.PanelOrigin(secondary, largePanel, offLeft with { Hidden = false }, 1.5) == new ScreenPoint(-2554, -114),
    "Negative virtual coordinates and DPI-scaled margins are preserved");
Expect(Docking.BookmarkOrigin(secondary, new(bookmark.Width * 1.5, bookmark.Height * 1.5), offLeft, 1.5).X == secondary.Left,
    "A left bookmark aligns to the secondary work area");

var samples = new DragVelocity();
samples.Start(new(50, 80), 100);
samples.Add(new(120, 80), 150);
Expect(samples.Add(new(170, 80), 200).X == 1200, "The recent mouse samples determine release speed");
Expect(samples.Add(new(170, 80), 410) == default, "A pause discards stale speed");

var directory = Path.Combine(Path.GetTempPath(), "zium-checks-" + Guid.NewGuid().ToString("N"));
try
{
    var file = Path.Combine(directory, "todo.json");
    var timestamp = DateTimeOffset.Parse("2026-11-25T12:00:00+09:00");
    var original = new TodoItem(Guid.NewGuid(), "견적서 회신", "금요일 전\n담당자에게 보내기", timestamp);
    var state = new TodoState([original], offLeft, false);
    var store = new TodoStore(file);
    Expect(store.Load().Status == LoadStatus.Normal, "New installs do not report missing data as an error");
    store.Save(state);
    var (loaded, status) = new TodoStore(file).Load();
    Expect(status == LoadStatus.Normal && loaded.Items.Single() == original && loaded.Position == offLeft && !loaded.Pinned,
        "Notes, completion, pin and dock all survive a restart");
    Expect(loaded.WindowSize is null, "Files created without saved dimensions use the default pane size");
    var extra = new TodoItem(Guid.NewGuid(), "바뀐 순서", "추가 메모", null);
    var changed = original with { Note = "이미 있던 메모 수정\n두 번째 줄" };
    store.Save(state with { Items = [extra, changed], WindowSize = bigger });
    var edited = new TodoStore(file).Load().State;
    Expect(edited.Items.SequenceEqual([extra, changed]) && edited.WindowSize == bigger,
        "A new list order, edited memo and resized pane survive a restart");
    File.WriteAllText(file, "incomplete json");
    var recovering = new TodoStore(file);
    var (recovered, recovery) = recovering.Load();
    Expect(recovery == LoadStatus.Recovered && recovered.Items.Single() == original, "A damaged save falls back to the previous copy");
    recovering.Save(state);
    Expect(new TodoStore(file).Load().Status == LoadStatus.Normal, "A successful save restores a readable primary file");
    Expect(!File.Exists(file + ".tmp"), "No temporary file remains after saving");
    File.WriteAllText(file, "damaged again");
    Expect(new TodoStore(file).Load().State.Items.Single() == original, "Recovery preserves the known good backup");
}
finally
{
    if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
}

Console.WriteLine("Zium core checks passed.");

static void Expect(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
}
