using System.Text.Json;
using PetTracker;

static class MerchantShortcutChecks
{
    public static void Run(Action<bool, string> check)
    {
        var old = JsonSerializer.Deserialize<AppSettings>("{\"Hotkey\":0,\"TrackerMode\":\"Nearby\"}")!;
        old.Normalize();
        check(HideShortcut.From(old) == new HideShortcut(0x44, 1), "Existing settings gain Alt+D without enabling the legacy shortcut");
        var custom = SettingsPatch.Apply(old, SettingsPatch.Fields((nameof(AppSettings.HideShortcutKey), 0x78), (nameof(AppSettings.HideShortcutModifiers), 6)));
        check(HideShortcut.From(custom).ToString() == "Ctrl + Shift + F9", "Custom modifier and function-key shortcut survives a settings patch");
        check(HideShortcut.From(JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(custom))!) == HideShortcut.From(custom), "Shortcut survives settings-file serialization");
        var bad = SettingsPatch.Apply(custom, SettingsPatch.Fields((nameof(AppSettings.HideShortcutKey), 0x10), (nameof(AppSettings.HideShortcutModifiers), 0)));
        check(HideShortcut.From(bad) == new HideShortcut(0x44, 1), "Bare modifiers and unmodified keys cannot become global shortcuts");
        var disabled = SettingsPatch.Apply(custom, SettingsPatch.Fields((nameof(AppSettings.HideShortcutKey), 0)));
        check(HideShortcut.From(disabled) == new HideShortcut(0, 0), "Disabled shortcut clears its modifier flags");
        check(HideShortcut.From(SettingsPatch.Apply(custom, SettingsSections.Defaults("Hide Shortcut"))) == new HideShortcut(0x44, 1), "Shortcut section resets only to Alt+D");
        check(SettingsPatch.Apply(custom, SettingsPatch.Fields((nameof(AppSettings.TrackerMode), "Merchant"))).TrackerMode == "Merchant", "Merchant is a persistent supported main-screen mode");
        var when = DateTimeOffset.Parse("2030-01-01T07:30:00Z");
        var book = new MerchantStockBook();
        var special = new MarketStock(101, when, when.AddMinutes(30), new[] { new MarketOffer(1, 534760190, 1196, 80000) });
        var pets = new MarketStock(104, when.AddSeconds(1), special.RefreshUtc, new[] { new MarketOffer(2, 1, 1046, 70000) });
        book.Observe(special); book.Observe(pets);
        check(book.Read(when.AddMinutes(5)).Count == 2 && !special.Fresh(when.AddMinutes(5)), "Dedicated merchant observations retain both tabs after recommendations expire");
        check(book.Read(special.RefreshUtc).Count == 0 && book.Read(when.AddSeconds(-1)).Count == 0, "Merchant observations expire at the exact refresh boundary and reject clock rollback");
        book.Observe(pets with { SeenUtc = when.AddMinutes(31), RefreshUtc = when.AddMinutes(90) });
        check(book.Read(when.AddMinutes(32)).Single().Tab == 104, "A newly observed refresh cycle cannot retain another tab from the old cycle");
        book.Clear();
        check(book.Read(when.AddMinutes(32)).Count == 0, "Invalidating a merchant session clears every observed tab");
    }
}
