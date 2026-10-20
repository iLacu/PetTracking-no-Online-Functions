using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;

namespace PetTracker;

static class CaptureProbe
{
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr OpenSCManager(string? machine, string? database, uint access);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr OpenService(IntPtr manager, string name, uint access);
    [DllImport("advapi32.dll", SetLastError = true)]
    static extern bool QueryServiceStatus(IntPtr service, out ServiceStatus status);
    [DllImport("advapi32.dll")]
    static extern bool CloseServiceHandle(IntPtr handle);
    [StructLayout(LayoutKind.Sequential)]
    struct ServiceStatus { public uint Type, State, Controls, Win32Exit, SpecificExit, Checkpoint, WaitHint; }
    public static CaptureProbeResult Run()
    {
        if (!OperatingSystem.IsWindows()) return new(false, false, false, 0, false, false, "WindowsRequired");
        bool installed = false, running = false, adminOnly = false, elevated;
        using (var identity = WindowsIdentity.GetCurrent())
            elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\npcap")) installed = key != null;
        using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\npcap\Parameters")) adminOnly = Convert.ToInt32(key?.GetValue("AdminOnly", 0)) != 0;
        IntPtr manager = OpenSCManager(null, null, 1);
        if (manager != IntPtr.Zero)
        {
            try
            {
                IntPtr service = OpenService(manager, "npcap", 4);
                if (service != IntPtr.Zero)
                {
                    try { installed = true; running = QueryServiceStatus(service, out var status) && status.State == 4; }
                    finally { CloseServiceHandle(service); }
                }
            }
            finally { CloseServiceHandle(manager); }
        }
        bool library = File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "Npcap", "wpcap.dll"));
        installed |= library;
        int adapters = 0;
        string? error = null;
        if (installed && running && library)
        {
            try { adapters = SharpPcap.LibPcap.LibPcapLiveDeviceList.Instance.Count; }
            catch (Exception ex) { library = false; error = ex.GetType().Name; }
        }
        return new(installed, running, library, adapters, adminOnly, elevated, error);
    }
}
