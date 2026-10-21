namespace PetTracker;

public sealed record MerchantRefreshNotice(bool Visible, bool Yellow, bool Blinking, DateTimeOffset? RefreshedAt);

// A local-clock reminder. It neither queries the game nor claims to have read stock.
public sealed class MerchantRefreshReminder(TimeZoneInfo? zone = null)
{
    public static readonly TimeSpan NoticeDuration = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan FlashPhase = TimeSpan.FromMilliseconds(500);
    public const int FlashCount = 5;
    readonly TimeZoneInfo timeZone = zone ?? TimeZoneInfo.Local;
    DateTimeOffset? watermark, started;

    public MerchantRefreshNotice Tick(DateTimeOffset now, bool enabled = true)
    {
        var local = TimeZoneInfo.ConvertTime(now, timeZone);
        var hour = new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, 0, 0, local.Offset);
        if (watermark == null) watermark = hour;
        else if (hour > watermark)
        {
            watermark = hour;
            if (enabled) started = now;
        }
        // Do not replay reminders when enabled or repeat a boundary after clock rollback.
        if (!enabled || started is { } previous && now < previous) started = null;
        bool visible = started is { } at && now >= at && now - at < NoticeDuration;
        double phase = visible ? Math.Floor((now - started!.Value).TotalMilliseconds / FlashPhase.TotalMilliseconds) : -1;
        bool blinking = phase is >= 0 and < FlashCount * 2;
        return new(visible, blinking && (int)phase % 2 == 0, blinking, started);
    }

    public DateTimeOffset NextRefresh(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, timeZone);
        var localHour = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified);
        if (timeZone.IsAmbiguousTime(localHour))
        {
            var repeated = timeZone.GetAmbiguousTimeOffsets(localHour).Select(offset => new DateTimeOffset(localHour, offset)).Where(t => t > now).OrderBy(t => t).ToArray();
            if (repeated.Length > 0) return repeated[0];
        }
        var next = localHour.AddHours(1);
        while (timeZone.IsInvalidTime(next)) next = next.AddMinutes(1);
        if (timeZone.IsAmbiguousTime(next))
            return timeZone.GetAmbiguousTimeOffsets(next).Select(offset => new DateTimeOffset(next, offset)).Where(t => t > now).OrderBy(t => t).First();
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(next, timeZone));
    }
}
