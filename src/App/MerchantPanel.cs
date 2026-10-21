using System.Windows.Threading;

namespace PetTracker;

public sealed class MerchantPanel : Border, IDisposable
{
    readonly Host host;
    readonly Func<DateTimeOffset> clock;
    readonly TextBlock countdown = Ui.Text("", 21), refreshTime = Ui.Text("", 10, Ui.Muted), status = Ui.Text("", 12, Ui.Muted);
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    bool disposed;
    public int OfferCount => 0;
    public MerchantPanel(Host host, Func<DateTimeOffset> clock)
    {
        this.host = host;
        this.clock = clock;
        Padding = new Thickness(4, 10, 4, 5);
        var content = new StackPanel();
        Child = content;
        content.Children.Add(Ui.Text("Black Cloud Merchant", 13));
        countdown.Margin = new Thickness(0, 7, 0, 2);
        countdown.FontWeight = FontWeights.SemiBold;
        System.Windows.Automation.AutomationProperties.SetAutomationId(countdown, "MerchantCountdown");
        content.Children.Add(countdown);
        content.Children.Add(refreshTime);
        status.Margin = new Thickness(0, 14, 0, 8);
        content.Children.Add(status);
        var help = Ui.Text("At each local clock hour, the Merchant tab flashes yellow five times and shows a refresh reminder for 30 seconds.", 12, Ui.Muted);
        help.Margin = new Thickness(0, 0, 0, 12);
        content.Children.Add(help);
        content.Children.Add(Ui.Button("Reminder settings", host.OpenPetOverlaySettings));
        timer.Tick += (s, e) => Refresh();
        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible && !disposed) { Refresh(); timer.Start(); }
            else timer.Stop();
        };
    }
    public void Refresh()
    {
        if (disposed) return;
        var now = clock();
        var next = host.MerchantReminder.NextRefresh(now);
        var left = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling((next - now).TotalSeconds)));
        countdown.Text = "Refresh in " + (left.TotalHours >= 1 ? left.ToString(@"hh\:mm\:ss") : left.ToString(@"mm\:ss"));
        countdown.Foreground = NativeTheme.Accent(host.PetOverlayPreferences.Accent);
        refreshTime.Text = "Next hourly reminder · " + next.ToLocalTime().ToString("HH:mm") + " · local time";
        status.Text = host.PetOverlayPreferences.MerchantRefreshAlerts ? "Hourly reminder is enabled." : "Hourly reminder is disabled.";
    }
    public void Dispose() { disposed = true; timer.Stop(); }
}
