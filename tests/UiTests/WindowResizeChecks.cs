using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;

namespace PetTracker;

static class WindowResizeChecks
{
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public NativeRect Bounds, Work; public int Flags; }
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window, int message, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out NativeRect bounds);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }
    static T Id<T>(DependencyObject root, string id) where T : DependencyObject => Children(root).OfType<T>().Single(e => System.Windows.Automation.AutomationProperties.GetAutomationId(e) == id);
    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    public static void Run(Action<bool, string> check, string folder, Action<Window> show, Action<int> pump)
    {
        using var host = new Host(Path.Combine(folder, "window-resize"), false);
        var profile = new Profile { CharacterName = "Resize simulation", Pets = Catalog.Create() };
        int[] ids = { 1046, 1128, 1148, 1050, 1060 };
        var now = DateTimeOffset.UtcNow;
        var view = new WorkerView(3, profile, true, "Simulation", "Resize fixtures", "Resize simulation", new(), "resize", ids.Select(id => new RecentPetActivity(id, null, "", now)).ToList(), null, now, 1, 0, 0, 0, new() { ExpireInactive = false, ReducedMotion = true });
        host.Preview(view);
        var overlay = host.Overlay!;
        check(overlay.ShowInTaskbar && overlay.ResizeMode == ResizeMode.CanResize, "Tracker has a taskbar entry and native minimize/maximize/resize window styles");
        show(overlay); pump(30);
        var handle = new WindowInteropHelper(overlay).Handle;
        check((GetWindowLong(handle, -16).ToInt64() & 0x50000) == 0x50000, "Native HWND has a sizing frame and maximize box");
        GetWindowRect(handle, out var rectangle);
        int midX = (rectangle.Left + rectangle.Right) / 2, midY = (rectangle.Top + rectangle.Bottom) / 2;
        var edges = new[] { (rectangle.Left + 1, midY, 10), (rectangle.Right - 2, midY, 11), (midX, rectangle.Top + 1, 12), (midX, rectangle.Bottom - 2, 15), (rectangle.Left + 1, rectangle.Top + 1, 13), (rectangle.Right - 2, rectangle.Top + 1, 14), (rectangle.Left + 1, rectangle.Bottom - 2, 16), (rectangle.Right - 2, rectangle.Bottom - 2, 17) };
        foreach (var (x, y, expected) in edges)
        {
            int hit = SendMessage(handle, 0x84, IntPtr.Zero, new IntPtr((y << 16) | (x & 0xffff))).ToInt32();
            check(hit == expected, "Native hit testing exposes resize edge/corner " + expected + " (actual " + hit + ")");
        }
        int caption = SendMessage(handle, 0x84, IntPtr.Zero, new IntPtr(((rectangle.Top + 25) << 16) | ((rectangle.Left + 50) & 0xffff))).ToInt32();
        check(caption == 2, "Title area retains native move, double-click maximize and system-menu behavior");
        check(((SolidColorBrush)overlay.Background).Color.A > 0, "Resize margins are mouse-accessible on the transparent window");
        void Complete(Task<bool> task)
        {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < until) pump(10);
            check(task.IsCompleted && task.GetAwaiter().GetResult(), "Window geometry saves before shutdown");
        }
        SendMessage(handle, 0x231, IntPtr.Zero, IntPtr.Zero);
        overlay.SizeToContent = SizeToContent.Manual;
        overlay.Width = 540; overlay.Height = 260; pump(30);
        host.Preview(view);
        check(overlay.Width == 540 && overlay.Height == 260, "A status poll cannot reset dimensions during a native resize gesture");
        SendMessage(handle, 0x232, IntPtr.Zero, IntPtr.Zero);
        Complete(overlay.SaveWindowBeforeQuit());
        check(host.PetOverlayPreferences.TrackerWidth == 540 && host.PetOverlayPreferences.TrackerHeight == 260, "Completed resize persists the actual window dimensions");
        host.Preview(host.View!); pump(10);
        check(overlay.Width == 540 && overlay.Height == 260, "Subsequent status polling preserves the user size");
        check(Id<ScrollViewer>(overlay, "TrackerViewport").ScrollableHeight > 0, "A smaller window scrolls the pet list instead of clipping inaccessible rows");
        Program.Save(overlay, Path.Combine(folder, "tracker-resized.png"), 540, 260);
        // Hold opacity at zero even when settings polling reapplies its base
        // value. Native on-screen tests must never flash over the game.
        void HideNative(Window window) => window.BeginAnimation(UIElement.OpacityProperty, new System.Windows.Media.Animation.DoubleAnimation(0, TimeSpan.Zero));
        HideNative(overlay);
        var firstArea = OverlayGeometry.WorkAreas(overlay)[0];
        overlay.Left = firstArea.Left + 40; overlay.Top = firstArea.Top + 80; pump(20);
        Click(Id<Button>(overlay, "MaximizeTracker")); pump(30);
        check(overlay.WindowState == WindowState.Maximized, "Maximize button uses the Windows maximized state");
        var monitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(MonitorFromWindow(handle, 2), ref monitor); GetWindowRect(handle, out var maximized);
        check(maximized.Left >= monitor.Work.Left - 8 && maximized.Top >= monitor.Work.Top - 8 && maximized.Right <= monitor.Work.Right + 8 && maximized.Bottom <= monitor.Work.Bottom + 8, "Maximizing respects the monitor work area and taskbar");
        Complete(overlay.SaveWindowBeforeQuit());
        check(host.PetOverlayPreferences.TrackerMaximized && host.PetOverlayPreferences.TrackerWidth == 540, "Maximized state keeps the normal restore dimensions");
        Click(Id<Button>(overlay, "MaximizeTracker")); pump(30); Complete(overlay.SaveWindowBeforeQuit());
        check(overlay.WindowState == WindowState.Normal && Math.Abs(overlay.ActualWidth - 540) < 1 && !host.PetOverlayPreferences.TrackerMaximized, "Restore returns to the saved normal dimensions");
        Click(Id<Button>(overlay, "MinimizeTracker")); pump(20);
        host.Preview(host.View!);
        check(overlay.WindowState == WindowState.Minimized, "Status polling leaves a minimized tracker minimized");
        host.TogglePetOverlay(); pump(20);
        check(overlay.WindowState == WindowState.Normal && host.PetOverlayPreferences.Visible, "The hide shortcut restores a minimized tracker on its first press");
        Complete(overlay.SaveWindowBeforeQuit());
        host.Change(new("patch", Patch: SettingsPatch.Fields((nameof(AppSettings.Locked), true)))).GetAwaiter().GetResult();
        check(overlay.ResizeMode == ResizeMode.CanMinimize && !Id<Button>(overlay, "MaximizeTracker").IsEnabled && WindowChrome.GetWindowChrome(overlay).ResizeBorderThickness.Left == 0, "Fixed-position mode deliberately disables size changes and maximization");
        host.Change(new("patch", Patch: SettingsPatch.Fields((nameof(AppSettings.Locked), false)))).GetAwaiter().GetResult();
        check(overlay.ResizeMode == ResizeMode.CanResize, "Unlocking restores native resize behavior");
        var settings = new SettingsWindow(host); show(settings); settings.ShowSection("Overlay"); pump(20);
        Click(Children(settings).OfType<Button>().Single(b => Equals(b.Content, "Reset window size"))); pump(350);
        check(host.PetOverlayPreferences.TrackerWidth == null && host.PetOverlayPreferences.TrackerHeight == null && overlay.SizeToContent == SizeToContent.Height, "Reset window size restores automatic sizing through the actual settings control");
        settings.Close();

        using var restoredHost = new Host(Path.Combine(folder, "window-resize-restored"), false);
        restoredHost.Preview(view with { Settings = new() { TrackerWidth = 460, TrackerHeight = 320, TrackerMaximized = true } });
        var restored = restoredHost.Overlay!; HideNative(restored); show(restored); pump(30);
        check(restored.WindowState == WindowState.Maximized, "A fresh window restores the previously saved maximized state");
        SystemCommands.RestoreWindow(restored); pump(30);
        check(Math.Abs(restored.ActualWidth - 460) < 1 && Math.Abs(restored.ActualHeight - 320) < 1, "Restoring after a restart retains the saved normal width and height");
        Complete(restored.SaveWindowBeforeQuit());

        using var automaticHost = new Host(Path.Combine(folder, "window-resize-automatic"), false);
        automaticHost.Preview(view);
        var automatic = automaticHost.Overlay!; HideNative(automatic); show(automatic); pump(30);
        // Normal tests live outside every monitor. Windows chooses a fallback
        // display for such windows; exercise maximize from a real work area.
        var automaticArea = OverlayGeometry.WorkAreas(automatic)[0];
        automatic.Left = automaticArea.Left + 40; automatic.Top = automaticArea.Top + 80; pump(20);
        Click(Id<Button>(automatic, "MaximizeTracker")); pump(30);
        var automaticHandle = new WindowInteropHelper(automatic).Handle;
        var automaticMonitor = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        GetMonitorInfo(MonitorFromWindow(automaticHandle, 2), ref automaticMonitor);
        GetWindowRect(automaticHandle, out var automaticBounds);
        check(automatic.WindowState == WindowState.Maximized && Math.Abs((automaticBounds.Bottom - automaticBounds.Top) - (automaticMonitor.Work.Bottom - automaticMonitor.Work.Top)) <= 16, $"A fresh automatically sized tracker maximizes to the full work area (state {automatic.WindowState}, bounds {automaticBounds.Left},{automaticBounds.Top},{automaticBounds.Right},{automaticBounds.Bottom}, work {automaticMonitor.Work.Left},{automaticMonitor.Work.Top},{automaticMonitor.Work.Right},{automaticMonitor.Work.Bottom}, size mode {automatic.SizeToContent}, WPF {automatic.ActualWidth}x{automatic.ActualHeight}, DPI {VisualTreeHelper.GetDpi(automatic)})");
        SendMessage(automaticHandle, 0xA3, new IntPtr(2), IntPtr.Zero); pump(30);
        check(automatic.WindowState == WindowState.Normal, "Native title double-click restores a maximized tracker");
        foreach (var area in OverlayGeometry.WorkAreas(automatic))
        {
            automatic.Left = area.Left + 40; automatic.Top = area.Top + 80; pump(20);
            var expected = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            GetMonitorInfo(MonitorFromWindow(automaticHandle, 2), ref expected);
            Click(Id<Button>(automatic, "MaximizeTracker")); pump(30);
            GetWindowRect(automaticHandle, out var bounds);
            check(Math.Abs(bounds.Left - expected.Work.Left) <= 8 && Math.Abs(bounds.Top - expected.Work.Top) <= 8 && Math.Abs(bounds.Right - expected.Work.Right) <= 8 && Math.Abs(bounds.Bottom - expected.Work.Bottom) <= 8, $"Maximizing fills the original monitor work area without jumping to another screen (expected {expected.Work.Left},{expected.Work.Top},{expected.Work.Right},{expected.Work.Bottom}; actual {bounds.Left},{bounds.Top},{bounds.Right},{bounds.Bottom})");
            SystemCommands.RestoreWindow(automatic); pump(30);
        }
        Complete(automatic.SaveWindowBeforeQuit());
    }
}
