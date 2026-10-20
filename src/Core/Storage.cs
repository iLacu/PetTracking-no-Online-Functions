using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PetTracker;

public static class Storage
{
    // Installed copy: EXE and worker beside each other. Portable friends package:
    // main EXE at the top, worker and all technical files below Code/Anwendung.
    public static string ProgramRoot => Directory.Exists(Path.Combine(AppContext.BaseDirectory, "Code", "Anwendung")) ? Path.Combine(AppContext.BaseDirectory, "Code", "Anwendung") : AppContext.BaseDirectory;
    public static string Root(string[] args)
    {
        int i = Array.IndexOf(args, "--data");
        return i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : Path.GetFullPath(Path.Combine(ProgramRoot, "..", "Daten"));
    }

    public static string Pipe(string root)
    {
        string path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return PipeHash(OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path);
    }

    // Compatibility probe only: do not use the old spelling-sensitive name to
    // create a new worker. Existing workers can still be quit through this pipe.
    public static string LegacyPipe(string root) => PipeHash(Path.GetFullPath(root));
    static string PipeHash(string root) => "A2PetTracking-Lacu-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName + root)))[..20];
    public static T Load<T>(string path, Func<T> fallback)
    {
        if (!File.Exists(path))
            return fallback();
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path)) ?? throw new InvalidDataException("The saved file is empty.");
    }

    public static void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(path))
            File.Replace(tmp, path, path + ".bak");
        else
            File.Move(tmp, path);
    }
}
