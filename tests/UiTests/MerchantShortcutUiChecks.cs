using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PetTracker;

static class MerchantShortcutUiChecks
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr FindWindow(string? cls, string name);
    [DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr window, int message, IntPtr w, IntPtr l);
    static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }
    static T Id<T>(DependencyObject root, string id) where T : DependencyObject => Children(root).OfType<T>().Single(e => System.Windows.Automation.AutomationProperties.GetAutomationId(e) == id);
    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    public static void Run(Action<bool, string> check, string folder, Action<Window> show, Action<int> pump)
    {
        using var host = new Host(Path.Combine(folder, "merchant-shortcut"), false);
        var profile = new Profile { CharacterName = "Simulation", Pets = Catalog.Create() };
        profile.Pets[1196].Level = 1; profile.Pets[1196].LevelVerified = true;
        profile.Pets[1046].Level = 3; profile.Pets[1046].LevelVerified = true;
        var time = new DateTimeOffset(2030, 1, 1, 11, 30, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2030, 1, 1)));
        var special = new MarketStock(101, time, time.AddMinutes(30), new[] { new MarketOffer(1, 534760190, 1196, 80000) });
        var pets = new MarketStock(104, time, special.RefreshUtc, new[] { new MarketOffer(2, 1, 1046, 70000) });
        var settings = new AppSettings { TrackerMode = "Merchant", HideWhenEmpty = true, HideMaxed = true, ReducedMotion = true, HideShortcutKey = 0 };
        var view = new WorkerView(3, profile, true, "Simulation", "Artificial shop observations", "Simulation", new(), "fixture", new(), null, time, 1, 0, 0, 0, settings, CollectionSynced: true, MerchantStock: new[] { special, pets });
        host.Preview(view);
        var overlay = new PetTrackerOverlay(host, () => time, false);
        try
        {
            show(overlay); pump(30);
            check(Id<Button>(overlay, "TrackerMode-Merchant").IsVisible && overlay.DisplayedPetCount == 0, "Merchant has its own main-screen tab and hides farming rows");
            check(Id<TextBlock>(overlay, "MerchantCountdown").Text == "Refresh in 30:00", "Merchant countdown follows the next full local clock hour");
            check(Children(overlay).OfType<TextBlock>().Any(t => t.IsVisible && t.Text == "Hourly reminder is enabled."), "Merchant page explains the enabled reminder");
            check(!Children(overlay).OfType<Button>().Any(b => System.Windows.Automation.AutomationProperties.GetAutomationId(b).StartsWith("MerchantTab-")) && overlay.DisplayedMerchantCount == 0, "Merchant reminder does not present old inventory tabs as current shop data");
            Program.Save(overlay, Path.Combine(folder, "merchant-reminder-page.png"), 400, 350);
            time = time.AddMinutes(5); overlay.Refresh(); pump(20);
            check(Id<TextBlock>(overlay, "MerchantCountdown").Text == "Refresh in 25:00", "Local reminder countdown advances normally without stock updates");
            time = special.RefreshUtc; overlay.Refresh(); pump(20);
            check(overlay.DisplayedMerchantCount == 0 && !Children(overlay).OfType<Grid>().Any(g => System.Windows.Automation.AutomationProperties.GetAutomationId(g).StartsWith("MerchantOffer-")), "At refresh the old prices disappear rather than becoming a new inventory");
            check(Id<TextBlock>(overlay, "MerchantCountdown").Text == "Refresh in 01:00:00" && Children(overlay).OfType<TextBlock>().Any(t => t.Text.StartsWith("Next hourly reminder")), "At the hour the countdown moves to the next hourly reminder");
            time = special.SeenUtc;
            host.Preview(view with { MerchantStock = new[] { special } }); overlay.Refresh();
            check(overlay.DisplayedMerchantCount == 0, "Reading Special never populates the unread Pets tab");
            host.Preview(view with { Ready = false, MerchantStock = new[] { special, pets } }); overlay.Refresh();
            check(overlay.DisplayedMerchantCount == 0, "A disconnected view cannot retain observed merchant stock");
            host.Preview(view); overlay.Refresh();
            Click(Id<Button>(overlay, "TrackerMode-Nearby")); overlay.Refresh(); pump(20);
            check(host.PetOverlayPreferences.TrackerMode == "Nearby" && !Id<TextBlock>(overlay, "MerchantCountdown").IsVisible, "Returning to Nearby hides the Merchant page");
        }
        finally { overlay.DisposeOverlay(); }

        var options = new SettingsWindow(host);
        show(options); options.ShowSection("Hide Shortcut"); pump(20);
        var button = Id<HideShortcutButton>(options, "HideShortcutButton");
        Click(Children(options).OfType<Button>().Single(b => Equals(b.Content, "Use Alt + D"))); pump(350);
        check(Equals(button.Content, "Alt + D") && HideShortcut.From(host.PetOverlayPreferences) == new HideShortcut(0x44, 1), "Hide Shortcut page restores its default Alt+D with a clickable button");
        Program.Save(options, Path.Combine(folder, "settings-hide-shortcut.png"), 820, 740);
        Click(button); pump(10);
        check(button.Capturing && host.RecordingShortcut, "Clicking the shortcut button starts focused recording and suspends global registration");
        check(!button.Capture(0x10, 0), "Pressing a modifier alone does not save a broken shortcut");
        check(button.Capture(0x78, 6), "Recorder accepts a custom Ctrl+Shift+F9 chord"); pump(350);
        check(!host.RecordingShortcut && HideShortcut.From(host.PetOverlayPreferences) == new HideShortcut(0x78, 6) && Equals(button.Content, "Ctrl + Shift + F9"), "Recorded key and modifiers save together through actual settings controls");
        Click(button); button.CancelCapture();
        check(!button.Capturing && Equals(button.Content, "Ctrl + Shift + F9"), "Cancel leaves the previous shortcut unchanged");
        using (var global = new GlobalHideShortcut(host))
        {
            check(host.HotkeyError == "", "Windows accepts the custom global shortcut");
            var handle = FindWindow(null, "A2Pet Tracking hide shortcut");
            check(handle != IntPtr.Zero, "Global shortcut owns its message window");
            show(host.Overlay!); pump(10);
            SendMessage(handle, 0x312, new IntPtr(1), IntPtr.Zero); pump(10);
            check(!host.PetOverlayPreferences.Visible && !host.Overlay!.IsVisible, "Actual WM_HOTKEY dispatch hides the tracker without ending it");
            SendMessage(handle, 0x312, new IntPtr(1), IntPtr.Zero); pump(10);
            check(host.PetOverlayPreferences.Visible, "A second global shortcut dispatch requests the tracker visible again");
            using var rival = new Host(Path.Combine(folder, "shortcut-conflict"), false);
            rival.Preview(view with { Settings = host.PetOverlayPreferences });
            using (var collision = new GlobalHideShortcut(rival))
                check(rival.HotkeyError.Contains("already in use"), "Windows shortcut conflicts are reported instead of silently failing");
            Click(button); pump(10);
            check(host.RecordingShortcut, "Existing registration can be suspended for rebinding");
            button.CancelCapture(); pump(10);
            check(host.HotkeyError == "", "Cancelling recording re-registers the previous shortcut");
        }
        using (var again = new GlobalHideShortcut(host))
            check(host.HotkeyError == "", "Disposing the shortcut releases its Windows registration");
        Click(Children(options).OfType<Button>().Single(b => Equals(b.Content, "Disable shortcut"))); pump(350);
        check(HideShortcut.From(host.PetOverlayPreferences) == new HideShortcut(0, 0), "Disable shortcut saves a fully disabled binding");
        options.Close();
    }
}
