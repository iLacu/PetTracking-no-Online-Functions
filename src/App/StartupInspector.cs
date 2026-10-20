using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PetTracker;

public static class StartupInspector
{
    public static async Task<StartupReport> Check(string programRoot, string executable, string dataRoot, CancellationToken stop = default)
    {
        var checks = new List<RequirementCheck>
        {
            new("windows", "Windows", OperatingSystem.IsWindows() && Environment.Is64BitProcess && RuntimeInformation.OSArchitecture == Architecture.X64,
                "Diese Ausgabe benötigt Windows auf einem x64-PC. Die .NET-Laufzeit ist bereits enthalten.")
        };
        var package = await Task.Run(() => StartupRequirements.Package(programRoot, executable), stop);
        checks.AddRange(package);
        if (checks.Any(c => !c.Passed)) return new(checks);
        checks.Add(StartupRequirements.WritableData(dataRoot));
        string temp = Path.Combine(Path.GetTempPath(), "A2Pet-Npcap-Check-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var start = new ProcessStartInfo(Path.Combine(programRoot, "A2PetTracking.Capture.exe")) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = programRoot };
            start.ArgumentList.Add("--check-npcap");
            start.ArgumentList.Add(temp);
            using var process = Process.Start(start) ?? throw new IOException();
            try { await process.WaitForExitAsync(stop).WaitAsync(TimeSpan.FromSeconds(25), stop); }
            catch { if (!process.HasExited) process.Kill(); throw; }
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(temp, stop));
            if (!document.RootElement.TryGetProperty("NpcapInstalled", out _)) throw new InvalidDataException();
            var capture = document.Deserialize<CaptureProbeResult>() ?? throw new InvalidDataException();
            checks.Add(new("npcap", "Npcap und Netzwerkadapter", capture.NpcapAvailable, capture.Description));
            return new(checks, !capture.NpcapInstalled);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or TimeoutException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            checks.Add(new("npcap", "Npcap und Netzwerkadapter", false, "Der lokale Npcap-Test konnte nicht ausgeführt werden. Programm vollständig entpacken, Dateizugriff prüfen und erneut testen. Es wurde nichts installiert."));
            return new(checks);
        }
        finally { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
}
