using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;

namespace PetTracker;

// Keep native Windows resize/move/system-menu behavior while retaining the
// translucent HUD. Persist completed gestures, never every WM_SIZE or poll.
public sealed class TrackerWindowFrame : IDisposable
{
    readonly Window window;
    readonly Host host;
    readonly WindowChrome chrome;
    readonly DispatcherTimer saveDelay = new() { Interval = TimeSpan.FromMilliseconds(250) };
    HwndSource? source;
    bool applying, saving, disposed;
    long generation, savedGeneration;
    Rect pendingBounds;
    bool pendingMaximized;
    public bool Adjusting { get; private set; }
    public bool Pending => generation != savedGeneration;
    public TrackerWindowFrame(Window window, Host host)
    {
        this.window = window; this.host = host;
        window.SourceInitialized += SourceReady;
        window.StateChanged += StateChanged;
        window.Loaded += Loaded;
        window.IsVisibleChanged += VisibilityChanged;
        chrome = new WindowChrome { CaptionHeight = 45, ResizeBorderThickness = new Thickness(7), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(8), UseAeroCaptionButtons = false };
        WindowChrome.SetWindowChrome(window, chrome);
        saveDelay.Tick += async (s, e) => { saveDelay.Stop(); await Flush(); };
    }
    void SourceReady(object? sender, EventArgs e)
    {
        source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        source?.AddHook(Message);
    }
    IntPtr Message(IntPtr handle, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        // Switch away from auto-height before Windows calculates maximized
        // bounds, including caption double-click and the system menu.
        if (message == 0x112 && (w.ToInt64() & 0xfff0) == 0xf030)
            window.SizeToContent = SizeToContent.Manual;
        if (message == 0x231) Adjusting = true; // WM_ENTERSIZEMOVE
        if (message == 0x232) // WM_EXITSIZEMOVE, including Windows snapping
        {
            Adjusting = false;
            Remember();
        }
        if (message == 0x24 && MonitorWorkArea(handle) is { } monitor) // WM_GETMINMAXINFO
        {
            var info = Marshal.PtrToStructure<MinMaxInfo>(l);
            info.MaxPosition = new(monitor.Work.Left - monitor.Bounds.Left, monitor.Work.Top - monitor.Bounds.Top);
            info.MaxSize = new(monitor.Work.Right - monitor.Work.Left, monitor.Work.Bottom - monitor.Work.Top);
            info.MaxTrackSize = new(Math.Max(info.MaxTrackSize.X, info.MaxSize.X), Math.Max(info.MaxTrackSize.Y, info.MaxSize.Y));
            double dpi = GetDpiForWindow(handle) / 96d;
            info.MinTrackSize = new(Math.Max(info.MinTrackSize.X, (int)Math.Ceiling(window.MinWidth * dpi)), Math.Max(info.MinTrackSize.Y, (int)Math.Ceiling(window.MinHeight * dpi)));
            Marshal.StructureToPtr(info, l, false);
            // WPF must still read these limits into its layout cache.
        }
        return IntPtr.Zero;
    }
    public void Apply(AppSettings settings)
    {
        if (disposed) return;
        applying = true;
        try
        {
            bool fixedWindow = settings.Locked || settings.ClickThrough;
            window.ResizeMode = fixedWindow ? ResizeMode.CanMinimize : ResizeMode.CanResize;
            chrome.CaptionHeight = fixedWindow ? 0 : 45 * settings.Scale;
            chrome.ResizeBorderThickness = new Thickness(fixedWindow ? 0 : 7);
            window.MinWidth = 300 * settings.Scale;
            window.MinHeight = 150 * settings.Scale;
            if (Adjusting || Pending || window.WindowState == WindowState.Minimized) return;
            if (settings.TrackerMaximized)
            {
                if (window.WindowState != WindowState.Maximized)
                {
                    ApplyNormalSize(settings);
                    if (!host.PreviewOnly) OverlayGeometry.RestorePosition(window, settings, OverlayGeometry.WorkAreas(window));
                    // WPF cannot initially Show a maximized window with
                    // ShowActivated=false. Restore state after Show has completed.
                    if (window.IsLoaded && window.IsVisible)
                    {
                        window.SizeToContent = SizeToContent.Manual;
                        window.WindowState = WindowState.Maximized;
                    }
                }
            }
            else
            {
                if (window.WindowState != WindowState.Normal) window.WindowState = WindowState.Normal;
                ApplyNormalSize(settings);
                if (!host.PreviewOnly) OverlayGeometry.RestorePosition(window, settings, OverlayGeometry.WorkAreas(window));
            }
        }
        finally { applying = false; }
    }
    void ApplyNormalSize(AppSettings settings)
    {
        window.Width = settings.TrackerWidth ?? (settings.Compact ? 300 : 340) * settings.Scale;
        window.SizeToContent = settings.TrackerHeight.HasValue ? SizeToContent.Manual : SizeToContent.Height;
        window.Height = settings.TrackerHeight ?? double.NaN;
    }
    void Loaded(object sender, RoutedEventArgs e) => ApplyAfterShow();
    void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (window.IsVisible) ApplyAfterShow();
    }
    void ApplyAfterShow() => window.Dispatcher.BeginInvoke(new Action(() => { if (!disposed && window.IsVisible) Apply(host.PetOverlayPreferences); }));
    void StateChanged(object? sender, EventArgs e)
    {
        if (window.WindowState == WindowState.Maximized) window.SizeToContent = SizeToContent.Manual;
        if (window.WindowState != WindowState.Minimized) Remember();
    }
    void Remember()
    {
        if (applying || disposed || source == null || window.WindowState == WindowState.Minimized) return;
        var bounds = window.WindowState == WindowState.Normal ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight) : window.RestoreBounds;
        if (!double.IsFinite(bounds.Left) || !double.IsFinite(bounds.Top) || bounds.Width < 1 || bounds.Height < 1) return;
        pendingBounds = bounds;
        pendingMaximized = window.WindowState == WindowState.Maximized;
        generation++;
        saveDelay.Stop(); saveDelay.Start();
    }
    public async Task<bool> Flush()
    {
        saveDelay.Stop();
        while (saving) await Task.Delay(20);
        if (!Pending || disposed) return true;
        saving = true;
        long current = generation;
        try
        {
            bool saved = await host.Change(new("patch", Patch: SettingsPatch.Fields(
                (nameof(AppSettings.Left), pendingBounds.Left), (nameof(AppSettings.Top), pendingBounds.Top),
                (nameof(AppSettings.TrackerWidth), pendingBounds.Width), (nameof(AppSettings.TrackerHeight), pendingBounds.Height),
                (nameof(AppSettings.TrackerMaximized), pendingMaximized))));
            if (saved) savedGeneration = current;
            return saved;
        }
        finally
        {
            saving = false;
            if (generation != current && !disposed) saveDelay.Start();
        }
    }
    public void Dispose()
    {
        disposed = true; saveDelay.Stop();
        source?.RemoveHook(Message);
        window.SourceInitialized -= SourceReady; window.StateChanged -= StateChanged;
        window.Loaded -= Loaded; window.IsVisibleChanged -= VisibilityChanged;
    }
    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X, Y; public NativePoint(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public NativeRect Bounds, Work; public int Flags; }
    [StructLayout(LayoutKind.Sequential)] struct MinMaxInfo { public NativePoint Reserved, MaxSize, MaxPosition, MinTrackSize, MaxTrackSize; }
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    static MonitorInfo? MonitorWorkArea(IntPtr window)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromWindow(window, 2), ref info) ? info : null;
    }
}
