using System.Windows.Input;

namespace PetTracker;

public sealed class HideShortcutButton : Button
{
    readonly Host host;
    readonly Action<int, int> save;
    HideShortcut value;
    public bool Capturing { get; private set; }
    public HideShortcutButton(Host host, Action<int, int> save)
    {
        this.host = host;
        this.save = save;
        SetResourceReference(StyleProperty, typeof(Button));
        Padding = new Thickness(20, 10, 20, 10);
        MinWidth = 190;
        HorizontalAlignment = HorizontalAlignment.Left;
        System.Windows.Automation.AutomationProperties.SetAutomationId(this, "HideShortcutButton");
        Click += (s, e) => BeginCapture();
        LostKeyboardFocus += (s, e) => CancelCapture();
        Update(host.PetOverlayPreferences);
    }
    public void Update(AppSettings settings)
    {
        value = HideShortcut.From(settings);
        if (!Capturing) Content = value.ToString();
    }
    public void BeginCapture()
    {
        Capturing = true;
        Content = "Press shortcut… (Esc cancels)";
        host.SetRecordingShortcut(true);
        Focus();
    }
    public void CancelCapture()
    {
        if (!Capturing) return;
        Capturing = false;
        Content = value.ToString();
        host.SetRecordingShortcut(false);
    }
    public bool Capture(int key, int modifiers)
    {
        if (!Capturing || key == 0 || !new HideShortcut(key, modifiers).Valid) return false;
        value = new(key, modifiers);
        save(key, modifiers);
        CancelCapture();
        return true;
    }
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!Capturing) { base.OnPreviewKeyDown(e); return; }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        e.Handled = true;
        if (key == Key.Escape) CancelCapture();
        else Capture(KeyInterop.VirtualKeyFromKey(key), (int)Keyboard.Modifiers);
    }
}
