using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace PetTracker;

public sealed record NpcapSetupResult(bool Completed, bool RestartRequired, string Message);
public static class NpcapSetup
{
    // Explicit dependency-install exception to the otherwise offline application.
    // No latest-version lookup, updater, telemetry or redistributable installer.
    // Reference download's Authenticode was Valid, Nmap Software LLC, 2026-10-20.
    public const string DownloadUrl = "https://npcap.com/dist/npcap-1.89.exe";
    public const string ExpectedSHA256 = "8AED85E900D783D1308506E919587D3E540451947AF8A82F2D04F819E44305CC";
    public const long ExpectedLength = 1325224;
    public static async Task WriteVerifiedDownload(Stream source, string destination, CancellationToken stop)
    {
        bool created = false;
        try
        {
            await using (var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                created = true;
                var buffer = new byte[81920];
                long total = 0;
                int count;
                while ((count = await source.ReadAsync(buffer, stop)) > 0)
                {
                    total += count;
                    if (total > ExpectedLength) throw new InvalidDataException("Der Npcap-Download hat eine unerwartete Größe. Es wurde nichts ausgeführt.");
                    await target.WriteAsync(buffer.AsMemory(0, count), stop);
                }
                if (total != ExpectedLength) throw new InvalidDataException("Der Npcap-Download ist unvollständig. Es wurde nichts ausgeführt.");
            }
            await using var verify = File.OpenRead(destination);
            if (!Convert.ToHexString(await SHA256.HashDataAsync(verify, stop)).Equals(ExpectedSHA256, StringComparison.Ordinal))
                throw new InvalidDataException("Die Npcap-Prüfsumme stimmt nicht. Es wurde nichts ausgeführt.");
        }
        catch { if (created) try { File.Delete(destination); } catch (IOException) { } catch (UnauthorizedAccessException) { } throw; }
    }
    public static async Task<NpcapSetupResult> Install(IProgress<string> progress, CancellationToken stop)
    {
        string folder = Path.Combine(Path.GetTempPath(), "A2Pet-Npcap-Setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, "npcap-1.89.exe");
        try
        {
            progress.Report("Der offizielle Npcap-Installer wird von npcap.com heruntergeladen und geprüft …");
            using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(120) };
            using var response = await client.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, stop);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength is { } size && size != ExpectedLength)
                throw new IOException("Der offizielle Npcap-Download ist zurzeit nicht verfügbar. Später erneut versuchen oder https://npcap.com/#download besuchen.");
            await using (var body = await response.Content.ReadAsStreamAsync(stop))
                await WriteVerifiedDownload(body, file, stop);
            stop.ThrowIfCancellationRequested();
            progress.Report("Npcap-Setup geöffnet. Bitte die Windows-Administratorabfrage bestätigen und die Installation im sichtbaren Setup abschließen. Die WinPcap-Kompatibilität aktiviert lassen.");
            // Hold a read lock from the final hash check through process exit, so
            // the downloaded file cannot be replaced between verification and run.
            using var locked = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (Convert.ToHexString(SHA256.HashData(locked)) != ExpectedSHA256) throw new InvalidDataException("Npcap-Datei verändert. Es wurde nichts ausgeführt.");
            using var process = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true, Verb = "runas", WorkingDirectory = folder }) ?? throw new IOException("Npcap-Setup konnte nicht gestartet werden.");
            // Never kill an installer midway through a driver installation.
            await process.WaitForExitAsync();
            return process.ExitCode switch
            {
                0 => new(true, false, "Npcap-Setup beendet. Die Installation wird erneut geprüft."),
                3010 => new(true, true, "Npcap wurde installiert. Bitte Windows neu starten und danach den Tracker öffnen."),
                350 => new(false, true, "Npcap benötigt einen Windows-Neustart. Danach das Setup erneut ausführen."),
                1 or 2 => new(false, false, "Npcap-Installation wurde abgebrochen. Der Tracker hat keine Erfassung gestartet."),
                1618 => new(false, false, "Eine andere Installation läuft bereits. Diese abschließen und erneut prüfen."),
                _ => new(false, false, "Npcap-Setup konnte nicht abgeschlossen werden (Code " + process.ExitCode + "). https://npcap.com/#download")
            };
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new(false, false, "Die Windows-Administratorabfrage wurde abgebrochen. Es wurde keine Erfassung gestartet.");
        }
        finally
        {
            try { File.Delete(file); Directory.Delete(folder); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
}
