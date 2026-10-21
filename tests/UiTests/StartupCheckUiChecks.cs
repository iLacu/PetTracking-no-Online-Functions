using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PetTracker;

static class StartupCheckUiChecks
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
    public static void Run(Action<bool, string> check, string folder, Action<Window> show, Action<int> pump)
    {
        var ready = new StartupReport(new[]
        {
            new RequirementCheck("windows", "Windows", true, "Windows x64 erkannt."),
            new RequirementCheck("package", "Programmdateien", true, "Programmdateien vollständig und unverändert."),
            new RequirementCheck("runtime", ".NET-Laufzeit", true, "Bereits in den EXE-Dateien enthalten."),
            new RequirementCheck("data", "Datenordner", true, "Sammlung und Einstellungen können lokal gespeichert werden."),
            new RequirementCheck("npcap", "Npcap und Netzwerkadapter", true, "Npcap bereit · 10 Netzwerkadapter erkannt.")
        });
        var missing = new StartupReport(ready.Checks.Select(c => c.Id == "npcap" ? c with { Passed = false, Detail = "Npcap fehlt. Der offizielle Installer wird heruntergeladen, geprüft und als sichtbares Setup geöffnet." } : c).ToArray(), true);
        void Complete(Task task)
        {
            var until = DateTime.UtcNow.AddSeconds(5);
            while (!task.IsCompleted && DateTime.UtcNow < until) pump(10);
            check(task.IsCompleted, "Startup workflow completes without a blocked UI thread");
            task.GetAwaiter().GetResult();
        }
        int installs = 0, inspections = 0;
        var happy = new StartupCheckWindow(_ => Task.FromResult(ready), (_, _) => { installs++; return Task.FromResult(new NpcapSetupResult(true, false, "")); }, false);
        show(happy); Complete(happy.RunCheck());
        check(happy.ReadyToStart && !happy.IsVisible && installs == 0, "Working Npcap proceeds immediately without downloading or reinstalling anything");

        var firstInstall = new StartupCheckWindow(_ => Task.FromResult(++inspections == 1 ? missing : ready), (_, _) => { installs++; return Task.FromResult(new NpcapSetupResult(true, false, "Done")); }, false);
        show(firstInstall); Complete(firstInstall.RunCheck());
        check(firstInstall.ReadyToStart && installs == 1 && inspections == 2, "Confirmed missing Npcap opens setup once, rechecks and proceeds only after success");

        var blocked = ready with { Checks = ready.Checks.Select(c => c.Id == "package" ? c with { Passed = false, Detail = "A2PetTracking.Capture.exe fehlt. Das vollständige ZIP erneut entpacken." } : c).ToArray(), NpcapMissing = true };
        var files = new StartupCheckWindow(_ => Task.FromResult(blocked), (_, _) => { installs++; return Task.FromResult(new NpcapSetupResult(true, false, "")); }, false);
        show(files); Complete(files.RunCheck());
        check(!files.ReadyToStart && installs == 1 && !Children(files).OfType<Button>().Any(b => Equals(b.Content, "Open Npcap setup") && b.IsVisible), "Missing program files prevent both tracking and unnecessary Npcap installation");
        Program.Save(files, Path.Combine(folder, "startup-files-missing.png"), 660, 570); files.Close();

        int attempts = 0;
        var canceled = new StartupCheckWindow(_ => Task.FromResult(missing), (_, _) => { attempts++; return Task.FromResult(new NpcapSetupResult(false, false, "Installation abgebrochen.")); }, false);
        show(canceled); Complete(canceled.RunCheck()); Complete(canceled.RunCheck());
        check(!canceled.ReadyToStart && attempts == 1 && canceled.IsVisible, "Cancelled installation does not loop or start tracking after a retry");
        check(Children(canceled).OfType<Button>().Any(b => Equals(b.Content, "Open Npcap setup") && b.IsVisible), "The user can retry visible setup after cancellation");
        Program.Save(canceled, Path.Combine(folder, "startup-npcap-missing.png"), 660, 570); canceled.Close();

        var reboot = new StartupCheckWindow(_ => Task.FromResult(missing), (_, _) => Task.FromResult(new NpcapSetupResult(true, true, "Bitte Windows neu starten.")), false);
        show(reboot); Complete(reboot.RunCheck());
        check(!reboot.ReadyToStart && Children(reboot).OfType<TextBlock>().Any(t => t.Text.Contains("Windows neu starten")), "An installer reboot result blocks capture and gives an explicit restart instruction"); reboot.Close();

        var corrupt = new StartupCheckWindow(_ => Task.FromResult(missing), (_, _) => throw new InvalidDataException("Die Npcap-Prüfsumme stimmt nicht."), false);
        show(corrupt); Complete(corrupt.RunCheck());
        check(!corrupt.ReadyToStart && Children(corrupt).OfType<TextBlock>().Any(t => t.Text.Contains("Prüfsumme")), "A rejected installer checksum produces an explanation and never starts capture"); corrupt.Close();

        string download = Path.Combine(folder, "rejected-installer.exe");
        foreach (int length in new[] { 12, (int)NpcapSetup.ExpectedLength, (int)NpcapSetup.ExpectedLength + 1 })
        {
            bool rejected = false;
            try { Task.Run(async () => { using var stream = new MemoryStream(new byte[length]); await NpcapSetup.WriteVerifiedDownload(stream, download, CancellationToken.None); }).GetAwaiter().GetResult(); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected && !File.Exists(download), "Untrusted installer length or content is rejected and removed: " + length);
        }
        File.WriteAllText(download, "keep existing file");
        try { Task.Run(async () => { using var stream = new MemoryStream(new byte[12]); await NpcapSetup.WriteVerifiedDownload(stream, download, CancellationToken.None); }).GetAwaiter().GetResult(); } catch (IOException) { }
        check(File.ReadAllText(download) == "keep existing file", "Download failure never deletes a pre-existing destination"); File.Delete(download);

        string? reference = Environment.GetEnvironmentVariable("A2PET_NPCAP_REFERENCE");
        if (!string.IsNullOrEmpty(reference) && File.Exists(reference))
        {
            Task.Run(async () => { using var stream = File.OpenRead(reference); await NpcapSetup.WriteVerifiedDownload(stream, download, CancellationToken.None); }).GetAwaiter().GetResult();
            check(new FileInfo(download).Length == NpcapSetup.ExpectedLength, "The verified official Npcap reference passes the exact pinned-download check without being executed");
            File.Delete(download);
        }
        check(typeof(Program).Assembly.GetManifestResourceNames().Contains("PetTracker.Icon") && typeof(Program).Assembly.GetManifestResourceNames().Contains("PetTracker.Portraits.1046.png"), "Icon and pet portraits are embedded, so they cannot be omitted during redistribution");
    }
}
