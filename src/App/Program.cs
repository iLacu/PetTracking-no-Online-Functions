using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace PetTracker;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        UiLanguage.Initialize(Storage.Root(args));
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(NativeTheme.Resources());
        if (args.Contains("--render"))
        {
            RenderPreview(args);
            return;
        }

        int startupCheck = Array.IndexOf(args, "--check-startup");
        if (startupCheck >= 0 && startupCheck + 1 < args.Length)
        {
            var report = Task.Run(() => StartupInspector.Check(Storage.ProgramRoot, Environment.ProcessPath!, Storage.Root(args))).GetAwaiter().GetResult();
            Storage.Save(Path.GetFullPath(args[startupCheck + 1]), report);
            Environment.ExitCode = report.CanStart ? 0 : 2;
            return;
        }

        if (args.Contains("--probe"))
        {
            int ix = Array.IndexOf(args, "--probe");
            string output = args[ix + 1];
            using var probe = new Host(Storage.Root(args), false);
            try
            {
                var result = Task.Run(async () =>
                {
                    var initial = await probe.Send(new("status"));
                    var created = await probe.Send(new("create", Name: "FriendTest", Realm: "Test server"));
                    var again = await probe.Send(new("status"));
                    await probe.Send(new("exit"));
                    return new
                    {
                        initial.Status,
                        initial.Devices,
                        Ready = again.Ready,
                        Profiles = again.Profiles.Count,
                        ReconnectPreserved = created.Selected == again.Selected
                    };
                }).GetAwaiter().GetResult();
                Storage.Save(output, result);
            }
            catch (Exception ex)
            {
                Storage.Save(output, new { Error = ex.Message });
            }

            return;
        }

        using var instance = new AppInstanceGate();
        if (!instance.IsPrimary || LegacyCopyRunning())
        {
            MessageBox.Show(UiLanguage.Text("A2 Pet Tracking by Lacu is already running. Use its paw icon in the Windows tray. To switch copies, choose Quit A2 Pet Tracking by Lacu there first."), "A2 Pet Tracking by Lacu", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string root = Storage.Root(args);
        using var mutex = new Mutex(true, "Local\\" + Storage.Pipe(root) + "-app", out bool first);
        if (!first)
            return;
        var prerequisites = new StartupCheckWindow(stop => StartupInspector.Check(Storage.ProgramRoot, Environment.ProcessPath!, root, stop));
        prerequisites.ShowDialog();
        if (!prerequisites.ReadyToStart)
            return;
        if (args.Contains("--startup-check-only"))
            return;
        using var host = OpenHost(root, args.Contains("--activity-check"));
        if (host == null)
            return;
        host.StartHidden = args.Contains("--minimized");
        using var tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "A2 Pet Tracking by Lacu",
            Visible = true,
            Icon = new System.Drawing.Icon(typeof(Program).Assembly.GetManifestResourceStream("PetTracker.Icon")!)
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        tray.ContextMenuStrip = menu;
        var menuLabels = new Dictionary<System.Windows.Forms.ToolStripItem, string>();
        void AddMenu(string text, EventHandler action) => menuLabels.Add(menu.Items.Add(UiLanguage.Text(text), null, action), text);
        void TranslateMenu() { foreach (var entry in menuLabels) entry.Key.Text = UiLanguage.Text(entry.Value); }
        UiLanguage.Changed += TranslateMenu;
        AddMenu("Show / hide tracker", (s, e) => host.TogglePetOverlay());
        AddMenu("Find a pet…", (s, e) => host.OpenPetFinder());
        AddMenu("Settings", (s, e) => host.OpenPetOverlaySettings());
        AddMenu("Unlock tracker", async (s, e) => await host.UnlockPetOverlay());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        AddMenu("Help", (s, e) => host.OpenConnectionHelp());
        bool quitting = false;
        async Task Quit()
        {
            if (quitting)
                return;
            quitting = true;
            try
            {
                await host.StopWorkerForQuit();
                tray.Visible = false;
                host.Dispose();
                app.Shutdown();
            }
            catch (Exception error)
            {
                Ui.Error(error);
            }
            finally
            {
                quitting = false;
            }
        }
        AddMenu("Quit A2 Pet Tracking by Lacu", async (s, e) => await Quit());
        host.QuitRequested += async () => await Quit();
        tray.DoubleClick += (s, e) => host.OpenPetOverlaySettings();
        using var shortcut = new GlobalHideShortcut(host);
        host.TestNotification += () => tray.ShowBalloonTip(4000, "A2 Pet Tracking by Lacu", UiLanguage.Text("Preview: your pet reached Rank 2."), System.Windows.Forms.ToolTipIcon.Info);
        host.Notification += text =>
        {
            if (host.PetOverlayPreferences.Toasts)
                tray.ShowBalloonTip(4000, "A2 Pet Tracking by Lacu", UiLanguage.Text(text), System.Windows.Forms.ToolTipIcon.Info);
        };
        app.DispatcherUnhandledException += (s, e) =>
        {
            e.Handled = true;
            Ui.Error(e.Exception);
        };
        app.Exit += (s, e) =>
        {
            shortcut.Dispose();
            UiLanguage.Changed -= TranslateMenu;
            tray.Visible = false;
        };
        app.Dispatcher.BeginInvoke(new Action(async () => await host.InitializeLaunch(args.Contains("--minimized"))));
        app.Run();
    }

    static Host? OpenHost(string root, bool activityCheck)
    {
        try
        {
            return new Host(root) { ActivityCheckOnStart = activityCheck };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(UiLanguage.Text("A2 Pet Tracking by Lacu couldn’t open its saved data. Close any program using the files, check folder access, then try again."), "A2 Pet Tracking by Lacu", MessageBoxButton.OK, MessageBoxImage.Warning);
            return null;
        }
    }

    static bool LegacyCopyRunning()
    {
        using var current = Process.GetCurrentProcess();
        foreach (var process in Process.GetProcessesByName("A2Pet Tracking").Concat(Process.GetProcessesByName("A2PetTracking")))
        {
            using (process)
                try
                {
                    if (process.Id == current.Id || process.SessionId != current.SessionId)
                        continue;
                    string? file = process.MainModule?.FileName;
                    if (file != null && FileVersionInfo.GetVersionInfo(file).ProductName == "A2 Pet Tracking by Lacu")
                        return true;
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
                catch (InvalidOperationException)
                {
                }
        }

        return false;
    }

    static void RenderPreview(string[] args)
    {
        int index = Array.IndexOf(args, "--render");
        string folder = Path.GetFullPath(args[index + 1]);
        Directory.CreateDirectory(folder);
        using var host = new Host(Path.Combine(folder, "fixture"), false);
        var profile = new Profile { CharacterName = "Ranger", Pets = Catalog.Create() };
        foreach (var p in profile.Pets.Values)
        {
            p.Level = 0;
            p.Souls = 0;
            p.Goal = 5;
            p.CounterVerified = true;
        }

        int[] ids = { 1050, 1060, 1007, 1219, 1141 };
        int[] values = { 72, 5, 3, 58, 75 };
        for (int i = 0; i < ids.Length; i++)
        {
            var p = profile.Pets[ids[i]];
            p.Level = i == 4 ? 3 : 2;
            p.Souls = i == 4 ? null : values[i];
            p.Goal = i == 4 ? null : 75;
            p.LevelVerified = true;
        }

        var now = DateTimeOffset.UtcNow;
        var recent = ids.Select((id, i) => new RecentPetActivity(id, null, "", now.AddSeconds(-i))).ToList();
        int languageIndex = Array.IndexOf(args, "--language");
        string language = languageIndex >= 0 && languageIndex + 1 < args.Length ? AppLanguage.Normalize(args[languageIndex + 1]) : "auto";
        var view = new WorkerView(3, profile, true, "Live", "Receiving soul updates from your character.", "Ranger", new() { new() { AccountName = "My account", CharacterName = "Ranger", RealmLabel = "Siel", ProfileKey = "1234567890abcdef12345678" } }, "1234567890abcdef12345678", recent, null, now, 1, 1000, 0, 0, new() { ExpireInactive = false, Language = language });
        host.Preview(view);
        Save(host.Overlay!, Path.Combine(folder, "tracker.png"), 340, 410);
        var settings = new SettingsWindow(host);
        Save(settings, Path.Combine(folder, "settings.png"), 780, 700);
        foreach (var section in new[] { "Tracking", "Overlay", "Hide Shortcut", "Account", "Language", "Updates", "Help" })
        {
            settings.ShowSection(section);
            Save(settings, Path.Combine(folder, "settings-" + section.Replace(" & ", "-").ToLowerInvariant() + ".png"), 780, 700);
        }

        settings.Close();
        var reminderTime = new DateTimeOffset(2030, 1, 1, 11, 59, 59, TimeSpan.Zero);
        using (var reminderPreview = new PreviewOverlayScope(new PetTrackerOverlay(host, () => reminderTime, false)))
        {
            reminderTime = reminderTime.AddSeconds(2);
            reminderPreview.Window.RefreshMerchantNotice();
            Save(reminderPreview.Window, Path.Combine(folder, "merchant-reminder.png"), 400, 600);
        }
        var startup = new StartupCheckWindow(_ => Task.FromResult(new StartupReport(Array.Empty<RequirementCheck>())), autoStart: false);
        var readyChecks = new[]
        {
            new RequirementCheck("windows", "Windows", true, "Windows x64 erkannt."),
            new RequirementCheck("package", "Programmdateien", true, "Alle benötigten Programmdateien sind vollständig und unverändert."),
            new RequirementCheck("runtime", ".NET-Laufzeit", true, "Bereits in den EXE-Dateien enthalten. Keine separate Installation nötig."),
            new RequirementCheck("data", "Datenordner", true, "Sammlung und Einstellungen können lokal gespeichert werden."),
            new RequirementCheck("npcap", "Npcap und Netzwerkadapter", true, "Npcap bereit · 10 Netzwerkadapter erkannt.")
        };
        startup.ShowReport(new(readyChecks));
        Save(startup, Path.Combine(folder, "startup-ready.png"), 660, 570);
        startup.ShowReport(new(readyChecks.Select(c => c.Id == "npcap" ? c with { Passed = false, Detail = "Npcap fehlt. Der offizielle Installer wird heruntergeladen und nach Prüfung geöffnet. Bitte das sichtbare Setup und die Windows-Administratorabfrage bestätigen." } : c).ToArray(), true));
        Save(startup, Path.Combine(folder, "startup-npcap-missing.png"), 660, 570);
        startup.Close();
    }

    public static void Save(Window window, string path, double width, double height)
    {
        var content = (FrameworkElement)window.Content;
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        content.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        var background = new DrawingVisual();
        using (var dc = background.RenderOpen())
            dc.DrawRectangle(Ui.Dark, null, new Rect(0, 0, width, height));
        bitmap.Render(background);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    sealed class PreviewOverlayScope(PetTrackerOverlay window) : IDisposable
    {
        public PetTrackerOverlay Window => window;
        public void Dispose() => Window.DisposeOverlay();
    }
}
