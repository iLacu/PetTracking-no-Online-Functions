using System.Security.Cryptography;
using System.Text.Json;
using PetTracker;

static class StartupRequirementChecks
{
    public static void Run(Action<bool, string> check, string folder)
    {
        string root = Path.Combine(folder, "startup-package");
        Directory.CreateDirectory(root);
        string main = Path.Combine(root, "main.exe"), worker = Path.Combine(root, "A2PetTracking.Capture.exe");
        File.WriteAllText(main, "fake executable for package validation"); File.WriteAllText(worker, "fake worker");
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        var manifest = new PackageManifest(1, "test", true, Hash(main), new() { ["A2PetTracking.Capture.exe"] = Hash(worker) });
        void Save(PackageManifest value) => File.WriteAllText(Path.Combine(root, StartupRequirements.ManifestName), JsonSerializer.Serialize(value));
        Save(manifest);
        check(StartupRequirements.Package(root, main).All(c => c.Passed), "Bundled package with matching main and worker hashes passes startup checks");
        File.WriteAllText(worker, "changed worker");
        check(StartupRequirements.Package(root, main).Any(c => !c.Passed && c.Detail.Contains("A2PetTracking.Capture.exe")), "Changed worker is blocked before it can run");
        File.Delete(worker);
        check(StartupRequirements.Package(root, main).Any(c => !c.Passed), "Missing worker produces a package error instead of starting tracking");
        File.WriteAllText(worker, "fake worker");
        File.WriteAllText(main, "changed main");
        check(StartupRequirements.Package(root, main).Any(c => !c.Passed), "Changed main executable is reported");
        Save(manifest with { Files = new() { ["A2PetTracking.Capture.exe"] = Hash(worker), ["..\\outside.txt"] = new string('0', 64) } });
        check(StartupRequirements.Package(root, main).Any(c => !c.Passed), "A manifest cannot inspect paths outside the program directory");
        Save(manifest with { BundledRuntime = false });
        check(StartupRequirements.Package(root, main).Any(c => !c.Passed), "A non-bundled manifest cannot claim that the runtime is present");
        File.WriteAllText(Path.Combine(root, StartupRequirements.ManifestName), "invalid json");
        check(StartupRequirements.Package(root, main).Any(c => !c.Passed), "Invalid manifest produces a readable package failure");
        File.Delete(Path.Combine(root, StartupRequirements.ManifestName));
        check(StartupRequirements.Package(root, main).Any(c => !c.Passed), "Copying only the EXE is diagnosed as an incomplete package");
        string data = Path.Combine(root, "Daten");
        check(StartupRequirements.WritableData(data).Passed && Directory.GetFiles(data).Length == 0, "Data write check leaves no probe file or fabricated collection");
        check(!StartupRequirements.WritableData(main).Passed, "Data path occupied by a file cannot pass the write check");
        var healthy = new CaptureProbeResult(true, true, true, 10, false, false);
        check(healthy.NpcapAvailable && !healthy.CaptureStarted, "Npcap readiness requires a real driver, library and adapters without starting capture");
        check(!(healthy with { DriverRunning = false }).NpcapAvailable && !(healthy with { LibraryLoaded = false }).NpcapAvailable && !(healthy with { AdapterCount = 0 }).NpcapAvailable, "Partial or broken Npcap installations do not pass");
        check(!(healthy with { AdminOnly = true }).NpcapAvailable && (healthy with { AdminOnly = true, Elevated = true }).NpcapAvailable, "Admin-only Npcap is explained for a non-elevated user");
        var missing = new StartupReport(new[] { new RequirementCheck("package", "Files", true, ""), new RequirementCheck("npcap", "Npcap", false, "") }, true);
        check(missing.CanInstallNpcap && !missing.CanStart, "Only a confirmed missing Npcap can trigger automatic setup");
        check(!(missing with { NpcapMissing = false }).CanInstallNpcap, "An unknown probe failure does not trigger an unnecessary installer");
        check(!(missing with { Checks = new[] { new RequirementCheck("package", "Files", false, ""), new RequirementCheck("npcap", "Npcap", false, "") } }).CanInstallNpcap, "Damaged program files block setup as well as tracking");
    }
}
