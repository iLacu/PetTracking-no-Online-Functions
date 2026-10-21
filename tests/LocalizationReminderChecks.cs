using System.Globalization;
using System.Text.Json;
using PetTracker;

static class LocalizationReminderChecks
{
    public static void Run(Action<bool, string> check)
    {
        var legacy = JsonSerializer.Deserialize<AppSettings>("{\"TrackerMode\":\"Nearby\"}")!;
        legacy.Normalize();
        check(legacy.Language == "auto" && legacy.MerchantRefreshAlerts, "Existing installations default to system language and enabled hourly reminders");
        foreach (string code in new[] { "de", "en", "es", "fr", "it", "pl" })
        {
            check(AppLanguage.Resolve("auto", CultureInfo.GetCultureInfo(code)) == code, "System language resolves " + code);
            var settings = SettingsPatch.Apply(legacy, SettingsPatch.Fields((nameof(AppSettings.Language), code), (nameof(AppSettings.MerchantRefreshAlerts), false)));
            var restored = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
            check(restored.Language == code && !restored.MerchantRefreshAlerts && restored.TrackerMode == "Nearby", "Language and reminder preference survive persistence without changing tracker mode: " + code);
            check(AppLanguage.Resolve(code, CultureInfo.GetCultureInfo("ja-JP")) == code, "Manual language overrides unsupported system language: " + code);
        }
        check(AppLanguage.Resolve(null, CultureInfo.GetCultureInfo("de-AT")) == "de" && AppLanguage.Resolve("auto", CultureInfo.GetCultureInfo("fr-CA")) == "fr", "Regional system cultures select the supported base language");
        check(AppLanguage.Resolve("auto", CultureInfo.GetCultureInfo("ja-JP")) == "en", "Unsupported system languages fall back to English");
        check(SettingsPatch.Apply(legacy, SettingsPatch.Fields((nameof(AppSettings.Language), "xx"))).Language == "auto", "Unsupported saved language resets to automatic selection");
        var changed = SettingsPatch.Apply(legacy, SettingsPatch.Fields((nameof(AppSettings.Language), "pl"), (nameof(AppSettings.MerchantRefreshAlerts), false)));
        var reset = SettingsPatch.Apply(changed, SettingsSections.Defaults("Language"));
        check(reset.Language == "auto" && !reset.MerchantRefreshAlerts && reset.TrackerMode == "Nearby", "Language reset affects only language");

        var before = new DateTimeOffset(2030, 1, 1, 11, 59, 59, TimeSpan.Zero);
        var at = before.AddSeconds(1);
        var reminder = new MerchantRefreshReminder(TimeZoneInfo.Utc);
        check(!reminder.Tick(before).Visible, "Starting the tracker does not replay the past hourly refresh");
        check(reminder.Tick(at).Visible && reminder.Tick(at).Yellow, "The exact hour starts one visible yellow reminder");
        int flashes = 0;
        bool prior = false;
        for (int phase = 0; phase <= 10; phase++)
        {
            var state = reminder.Tick(at.AddMilliseconds(phase * 500));
            if (state.Yellow && !prior) flashes++;
            prior = state.Yellow;
        }
        check(flashes == 5 && !reminder.Tick(at.AddSeconds(5)).Blinking, "There are exactly five yellow pulses and blinking stops after five seconds");
        check(reminder.Tick(at.AddSeconds(29.999)).Visible && !reminder.Tick(at.AddSeconds(30)).Visible, "The notice disappears at exactly thirty seconds");
        check(!reminder.Tick(at.AddMinutes(20)).Visible, "Ordinary polls cannot restart a completed hourly reminder");
        check(reminder.Tick(at.AddHours(1)).Visible && !reminder.Tick(at.AddHours(1).AddMilliseconds(100), false).Visible, "Disabling a reminder immediately clears its active notice");
        check(!reminder.Tick(at.AddHours(1).AddSeconds(1), true).Visible, "Re-enabling cannot replay the same hour");
        check(!reminder.Tick(at.AddHours(2), false).Visible && !reminder.Tick(at.AddHours(2).AddSeconds(1), true).Visible, "Disabled hourly boundaries are not queued for later");
        check(reminder.Tick(at.AddHours(3)).Visible, "The next enabled hour still produces a reminder");
        check(!reminder.Tick(at.AddHours(2)).Visible && !reminder.Tick(at.AddHours(3)).Visible, "Clock rollback cannot replay a notification");
        var skipped = reminder.Tick(at.AddHours(8));
        check(skipped.Visible && skipped.RefreshedAt == at.AddHours(8), "A suspended app resumes with one current reminder instead of replaying missed hours");
        check(reminder.NextRefresh(at.AddMinutes(10)) == at.AddHours(1), "Next reminder is the next whole local-clock hour");
        var nepal = TimeZoneInfo.CreateCustomTimeZone("Reminder +05:45", TimeSpan.FromMinutes(345), "Reminder +05:45", "Reminder +05:45");
        var localReminder = new MerchantRefreshReminder(nepal);
        var localBoundary = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.FromMinutes(345));
        localReminder.Tick(localBoundary.AddSeconds(-1));
        check(localReminder.Tick(localBoundary).Visible && localReminder.NextRefresh(localBoundary) == localBoundary.AddHours(1), "Whole local hours work in time zones with fractional UTC offsets");
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
        var dst = new MerchantRefreshReminder(berlin);
        var firstTwoThirty = new DateTimeOffset(2030, 10, 27, 2, 30, 0, TimeSpan.FromHours(2));
        check(dst.NextRefresh(firstTwoThirty) == new DateTimeOffset(2030, 10, 27, 2, 0, 0, TimeSpan.FromHours(1)), "Autumn clock changes include the repeated local hour");
    }
}
