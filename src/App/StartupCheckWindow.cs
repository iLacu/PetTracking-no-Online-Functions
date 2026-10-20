namespace PetTracker;

public sealed class StartupCheckWindow : Window
{
    readonly Func<CancellationToken, Task<StartupReport>> inspect;
    readonly Func<IProgress<string>, CancellationToken, Task<NpcapSetupResult>> install;
    readonly CancellationTokenSource lifetime = new();
    readonly StackPanel items = new();
    readonly TextBlock message = Ui.Text("Programmdateien und Npcap werden geprüft …", 12, Ui.Muted);
    readonly ProgressBar progress = new() { Height = 5, IsIndeterminate = true, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 14, 0, 18) };
    readonly Button retry, setup;
    bool busy, attemptedInstall, closed;
    public bool ReadyToStart { get; private set; }
    public StartupReport? Report { get; private set; }
    public StartupCheckWindow(Func<CancellationToken, Task<StartupReport>> inspect, Func<IProgress<string>, CancellationToken, Task<NpcapSetupResult>>? install = null, bool autoStart = true)
    {
        this.inspect = inspect;
        this.install = install ?? NpcapSetup.Install;
        Title = "A2Pet Tracking – Startprüfung";
        Width = 660; Height = 570; MinHeight = 470; MinWidth = 580;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        NativeTheme.Install(this); NativeTheme.DarkTitleBar(this);
        var root = new DockPanel { Margin = new Thickness(25) };
        Content = root;
        var header = new StackPanel();
        header.Children.Add(Ui.Text("Startprüfung", 25));
        var intro = Ui.Text("Die Voraussetzungen werden vor dem Tracker-Start lokal geprüft.", 12, Ui.Muted);
        intro.Margin = new Thickness(0, 7, 0, 0); header.Children.Add(intro);
        header.Children.Add(progress);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var footer = new StackPanel { Margin = new Thickness(0, 17, 0, 0) };
        footer.Children.Add(message);
        var actions = new WrapPanel { Margin = new Thickness(0, 15, 0, 0) };
        retry = Ui.Button("Erneut prüfen", async () => await RunCheck());
        setup = Ui.Button("Npcap-Setup öffnen", async () => await InstallNpcap());
        setup.Visibility = Visibility.Collapsed;
        actions.Children.Add(retry); actions.Children.Add(setup); actions.Children.Add(Ui.Button("Beenden", Close));
        footer.Children.Add(actions); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = items, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Closing += (s, e) => { closed = true; lifetime.Cancel(); };
        if (autoStart) Loaded += async (s, e) => await RunCheck();
    }
    public void ShowReport(StartupReport report)
    {
        Report = report;
        items.Children.Clear();
        foreach (var check in report.Checks)
        {
            var section = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            var title = Ui.Text((check.Passed ? "✓ " : "! ") + check.Name, 14, check.Passed ? new SolidColorBrush(Color.FromRgb(111, 202, 151)) : NativeTheme.Accent("Gold"));
            title.FontWeight = FontWeights.SemiBold; section.Children.Add(title);
            var detail = Ui.Text(check.Detail, 12, Ui.Muted); detail.Margin = new Thickness(17, 5, 0, 0); section.Children.Add(detail);
            items.Children.Add(section);
        }
        bool canRepair = report.Checks.Any(c => c.Id == "npcap" && !c.Passed) && report.Checks.Where(c => c.Id != "npcap").All(c => c.Passed);
        setup.Visibility = canRepair ? Visibility.Visible : Visibility.Collapsed;
        message.Text = report.CanStart ? "Prüfung erfolgreich. Tracker wird gestartet …" : "Der Tracker startet erst, wenn alle Voraussetzungen erfüllt sind.";
    }
    public async Task RunCheck()
    {
        if (busy || closed) return;
        Busy(true);
        bool shouldInstall = false;
        try
        {
            var report = await inspect(lifetime.Token);
            if (closed) return;
            ShowReport(report);
            if (report.CanStart) { ReadyToStart = true; Close(); }
            else shouldInstall = report.CanInstallNpcap && !attemptedInstall;
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!closed) message.Text = "Die Startprüfung konnte nicht abgeschlossen werden. Erneut prüfen oder das vollständige ZIP neu entpacken."; }
        finally { Busy(false); }
        if (shouldInstall && !closed) await InstallNpcap();
    }
    public async Task InstallNpcap()
    {
        if (busy || closed || Report == null || !Report.Checks.Any(c => c.Id == "npcap" && !c.Passed) || Report.Checks.Where(c => c.Id != "npcap").Any(c => !c.Passed)) return;
        attemptedInstall = true;
        Busy(true);
        bool verifyAgain = false;
        try
        {
            var result = await install(new Progress<string>(text => { if (!closed) message.Text = text; }), lifetime.Token);
            if (closed) return;
            message.Text = result.Message;
            verifyAgain = result.Completed && !result.RestartRequired;
        }
        catch (OperationCanceledException) { if (!closed) message.Text = "Npcap-Download abgebrochen."; }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or IOException or InvalidDataException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            if (!closed) message.Text = ex is InvalidDataException ? ex.Message : "Npcap konnte nicht geladen oder installiert werden. Internetverbindung prüfen oder den offiziellen Installer von https://npcap.com/#download verwenden.";
        }
        finally { Busy(false); }
        if (verifyAgain && !closed) await RunCheck();
    }
    void Busy(bool value)
    {
        busy = value; retry.IsEnabled = !value; setup.IsEnabled = !value;
        progress.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }
}
