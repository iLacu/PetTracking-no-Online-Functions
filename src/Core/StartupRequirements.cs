using System.Security.Cryptography;
using System.Text.Json;

namespace PetTracker;

public sealed record PackageManifest(int Format, string Version, bool BundledRuntime, string MainProgramSHA256, Dictionary<string, string> Files);
public sealed record RequirementCheck(string Id, string Name, bool Passed, string Detail);
public sealed record StartupReport(IReadOnlyList<RequirementCheck> Checks, bool NpcapMissing = false)
{
    public bool CanStart => Checks.Count > 0 && Checks.All(c => c.Passed);
    public bool CanInstallNpcap => NpcapMissing && Checks.Any(c => c.Id == "npcap" && !c.Passed) && Checks.Where(c => c.Id != "npcap").All(c => c.Passed);
}
public sealed record CaptureProbeResult(bool NpcapInstalled, bool DriverRunning, bool LibraryLoaded, int AdapterCount, bool AdminOnly, bool Elevated, string? Error = null)
{
    public bool NpcapAvailable => NpcapInstalled && DriverRunning && LibraryLoaded && AdapterCount > 0 && (!AdminOnly || Elevated);
    public bool CaptureStarted => false;
    public string Description => !NpcapInstalled ? "Npcap fehlt. Der offizielle Installer wird benötigt."
        : !DriverRunning ? "Npcap ist installiert, aber der Treiber läuft nicht. Windows neu starten oder Npcap reparieren."
        : AdminOnly && !Elevated ? "Npcap erlaubt nur Administratoren den Zugriff. Tracker als Administrator starten oder die Npcap-Option für Administratorzugriff im Setup ändern."
        : !LibraryLoaded ? "Die Npcap-Bibliothek kann nicht geladen werden. Npcap mit dem offiziellen Setup reparieren."
        : AdapterCount == 0 ? "Npcap wurde geladen, aber kein Netzwerkadapter gefunden. Netzwerkadapter aktivieren und erneut prüfen."
        : $"Npcap bereit · {AdapterCount} Netzwerkadapter erkannt.";
}

public static class StartupRequirements
{
    public const string ManifestName = "startpruefung.json";
    public static IReadOnlyList<RequirementCheck> Package(string directory, string executable)
    {
        var checks = new List<RequirementCheck>();
        try
        {
            string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            var manifest = JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(Path.Combine(root, ManifestName))) ?? throw new InvalidDataException();
            if (manifest.Format != 1 || !manifest.BundledRuntime || manifest.Files == null || !manifest.Files.ContainsKey("A2PetTracking.Capture.exe") || manifest.Files.Count is < 1 or > 2000)
                throw new InvalidDataException();
            bool valid = HashMatches(executable, manifest.MainProgramSHA256);
            var missing = new List<string>();
            if (!valid) missing.Add("A2Pet Tracking.exe");
            foreach (var file in manifest.Files)
            {
                string path = Path.GetFullPath(Path.Combine(root, file.Key));
                if (Path.IsPathRooted(file.Key) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException();
                if (!HashMatches(path, file.Value)) missing.Add(file.Key);
            }
            checks.Add(new("package", "Programmdateien", missing.Count == 0, missing.Count == 0 ? "Alle benötigten Programmdateien sind vollständig und unverändert." : "Datei fehlt oder ist beschädigt: " + string.Join(", ", missing.Take(5)) + ". Das vollständige ZIP erneut entpacken; die EXE allein reicht nicht."));
            checks.Add(new("runtime", ".NET-Laufzeit", true, "Bereits in den EXE-Dateien enthalten. Keine separate .NET-Installation nötig."));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            checks.Add(new("package", "Programmdateien", false, "Das Programmpaket ist unvollständig oder nicht lesbar. Das vollständige ZIP in einen beschreibbaren Ordner entpacken. Persönliche Daten vorher sichern."));
        }
        return checks;
    }
    static bool HashMatches(string path, string expected)
    {
        if (expected == null || expected.Length != 64 || !File.Exists(path)) return false;
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
    public static RequirementCheck WritableData(string directory)
    {
        string? probe = null;
        try
        {
            Directory.CreateDirectory(directory);
            probe = Path.Combine(directory, ".startpruefung-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            File.Delete(probe);
            return new("data", "Datenordner", true, "Sammlung und Einstellungen können lokal gespeichert werden.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new("data", "Datenordner", false, "Der Datenordner ist nicht beschreibbar. Das ZIP zum Beispiel nach Dokumente entpacken und erneut starten.");
        }
        finally
        {
            if (probe != null) try { File.Delete(probe); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
