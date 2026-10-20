using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PetTracker;

// RegisterHotKey handles a single chord; no keyboard hook or keystroke log.
public sealed class GlobalHideShortcut : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")]
    static extern bool UnregisterHotKey(IntPtr window, int id);
    readonly Host host;
    readonly HwndSource messages;
    HideShortcut? registered;
    bool disposed;
    public GlobalHideShortcut(Host host)
    {
        this.host = host;
        messages = new HwndSource(new HwndSourceParameters("A2Pet Tracking hide shortcut") { Width = 0, Height = 0, WindowStyle = 0 });
        messages.AddHook(Message);
        host.Changed += Update;
        Update();
    }
    IntPtr Message(IntPtr window, int message, IntPtr w, IntPtr l, ref bool handled)
    {
        if (message == 0x312 && w.ToInt32() == 1)
        {
            if (!host.RecordingShortcut) host.TogglePetOverlay();
            handled = true;
        }
        return IntPtr.Zero;
    }
    void Update()
    {
        if (disposed) return;
        var next = host.RecordingShortcut ? new HideShortcut(0, 0) : HideShortcut.From(host.PetOverlayPreferences);
        if (registered == next) return;
        UnregisterHotKey(messages.Handle, 1);
        registered = next;
        host.SetHotkeyError("");
        if (next.Key != 0 && !RegisterHotKey(messages.Handle, 1, (uint)next.Modifiers | 0x4000, (uint)next.Key))
            host.SetHotkeyError(next + " is already in use. Choose another shortcut.");
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        host.Changed -= Update;
        UnregisterHotKey(messages.Handle, 1);
        messages.Dispose();
    }
}
