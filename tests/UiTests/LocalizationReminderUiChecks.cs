using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PetTracker;

static class LocalizationReminderUiChecks
{
    static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }
    static T Id<T>(DependencyObject root, string id) where T : DependencyObject => Children(root).OfType<T>().Single(c => System.Windows.Automation.AutomationProperties.GetAutomationId(c) == id);
    public static void Run(Action<bool, string> check, string folder, Action<Window> show, Action<int> pump)
    {
        using var host = new Host(Path.Combine(folder, "language-reminder"), false);
        var view = new WorkerView(3, new Profile { Pets = Catalog.Create() }, false, "Waiting for Aion 2", "Open Aion 2 to connect your character.", "", new(), null, new(), null, null, 0, 0, 0, 0, new AppSettings { Language = "en", TrackerMode = "Recent" });
        host.Preview(view);
        var settings = new SettingsWindow(host);
        try
        {
            show(settings); settings.ShowSection("Language"); pump(20);
            var choice = Id<ComboBox>(settings, "LanguageChoice");
            check(choice.Items.Count == 7 && choice.IsVisible, "Language page has automatic selection plus all six languages");
            foreach (var (code, title) in new[] { ("de", "Einstellungen"), ("en", "Settings"), ("es", "Ajustes"), ("fr", "Paramètres"), ("it", "Impostazioni"), ("pl", "Ustawienia") })
            {
                choice.SelectedItem = AppLanguage.Options.First(o => o.Code == code); pump(350);
                check(host.PetOverlayPreferences.Language == code && UiLanguage.Current == code, "Actual dropdown persists and applies " + code);
                check(settings.Title.EndsWith(title) && Children(settings).OfType<TextBlock>().Any(t => t.Text == title), "The same open settings window changes its title and labels: " + code);
                check(host.PetOverlayPreferences.TrackerMode == "Recent", "Translated captions cannot alter stable stored mode values: " + code);
                Program.Save(settings, Path.Combine(folder, "language-" + code + ".png"), 820, 740);
                check(UiLanguage.Text("Rank 2") != "Rank 2" || code == "en", "Dynamic rank text is translated: " + code);
                check(UiLanguage.Text("✓ Programmdateien") == "✓ " + UiLanguage.Text("Program files"), "Legacy prerequisite labels follow language choice: " + code);
            }
            foreach (var entry in UiLanguage.Catalog)
            {
                var expected = Regex.Matches(entry.Key, @"\{\d\}").Select(m => m.Value).Distinct().Order().ToArray();
                check(entry.Value.Length == 5 && entry.Value.All(v => !string.IsNullOrWhiteSpace(v) && Regex.Matches(v, @"\{\d\}").Select(m => m.Value).Distinct().Order().SequenceEqual(expected)), "Complete translation and matching format parameters: " + entry.Key);
            }
            choice.SelectedItem = AppLanguage.Options[0]; pump(350);
            check(host.PetOverlayPreferences.Language == "auto" && UiLanguage.Current == "en", "Automatic selection returns to the test process system UI culture");
            settings.ShowSection("Tracking"); pump(20);
            var enabled = Id<CheckBox>(settings, nameof(AppSettings.MerchantRefreshAlerts));
            check(enabled.IsChecked == true, "Hourly reminder has its own enabled-by-default checkbox");
            var clock = new DateTimeOffset(2030, 1, 1, 11, 59, 59, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2030, 1, 1)));
            var overlay = new PetTrackerOverlay(host, () => clock, false);
            try
            {
                show(overlay); pump(20);
                var notice = Id<Border>(overlay, "MerchantRefreshNotice");
                var tab = Id<Button>(overlay, "TrackerMode-Merchant");
                check(!notice.IsVisible, "No fabricated refresh notice appears when opening the tracker");
                clock = clock.AddSeconds(1); overlay.RefreshMerchantNotice(); pump(10);
                check(notice.IsVisible && ((SolidColorBrush)tab.Background).Color == Color.FromRgb(255, 215, 80), "Actual Merchant tab flashes yellow with another tab selected and no game connection");
                Program.Save(overlay, Path.Combine(folder, "merchant-hourly-notice.png"), 400, 320);
                clock = clock.AddMilliseconds(500); overlay.RefreshMerchantNotice();
                check(((SolidColorBrush)tab.Background).Color.A == 0 && notice.IsVisible, "Blink off phase leaves the thirty-second notice visible");
                enabled.IsChecked = false; pump(350); overlay.RefreshMerchantNotice();
                check(!host.PetOverlayPreferences.MerchantRefreshAlerts && !notice.IsVisible, "Actual settings checkbox immediately stops an in-progress alarm");
                clock = clock.AddHours(1); overlay.RefreshMerchantNotice();
                enabled.IsChecked = true; pump(350); overlay.RefreshMerchantNotice();
                check(!notice.IsVisible, "Enabling the setting does not replay an hour that passed while disabled");
                clock = clock.AddHours(1); overlay.RefreshMerchantNotice();
                check(notice.IsVisible, "The next enabled local hour produces a notice again");
                clock = clock.AddSeconds(30); overlay.RefreshMerchantNotice();
                check(!notice.IsVisible, "Actual notice disappears after thirty seconds");
            }
            finally { overlay.DisposeOverlay(); }
        }
        finally { settings.Close(); UiLanguage.Use("en"); }
    }
}
