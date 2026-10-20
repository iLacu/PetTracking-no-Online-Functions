using System.Text.Json;
using PetTracker;

static class WindowSizeChecks
{
    public static void Run(Action<bool, string> check)
    {
        var old = JsonSerializer.Deserialize<AppSettings>("{\"Scale\":1.25,\"Compact\":true}")!;
        old.Normalize();
        check(old.TrackerWidth == null && old.TrackerHeight == null && !old.TrackerMaximized && old.Scale == 1.25, "Older settings keep automatic tracker sizing and their chosen text scale");
        var resized = SettingsPatch.Apply(old, SettingsPatch.Fields((nameof(AppSettings.TrackerWidth), 540), (nameof(AppSettings.TrackerHeight), 260), (nameof(AppSettings.TrackerMaximized), true)));
        var saved = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(resized))!;
        check(saved.TrackerWidth == 540 && saved.TrackerHeight == 260 && saved.TrackerMaximized && saved.Compact, "User window dimensions and maximized state survive settings-file serialization");
        resized.TrackerWidth = double.PositiveInfinity; resized.TrackerHeight = double.NaN; resized.Normalize();
        check(resized.TrackerWidth == null && resized.TrackerHeight == null, "Non-finite window dimensions cannot enter WPF layout");
        resized.TrackerWidth = -100; resized.TrackerHeight = 90000; resized.Normalize();
        check(resized.TrackerWidth == 240 && resized.TrackerHeight == 8192, "Invalid dimensions are bounded before window creation");
        var reset = SettingsPatch.Apply(saved, SettingsPatch.Fields((nameof(AppSettings.TrackerWidth), null), (nameof(AppSettings.TrackerHeight), null), (nameof(AppSettings.TrackerMaximized), false)));
        check(reset.TrackerWidth == null && reset.TrackerHeight == null && !reset.TrackerMaximized && reset.Scale == 1.25 && reset.Compact, "Resetting window size preserves unrelated appearance preferences");
    }
}
