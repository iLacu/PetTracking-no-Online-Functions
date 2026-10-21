using System.Text.Json;

namespace PetTracker;

public class AppSettings
{
    public string Language { get; set; } = "auto";
    public bool MerchantRefreshAlerts { get; set; } = true;
    public bool ShareAllCharacters { get; set; }
    // Retained for settings-file compatibility. Walking guidance is not ready for
    // distribution; neither old preferences nor IPC patches may enable it.
    public bool ExperimentalDirections { get; set; }
    public string TrackerMode { get; set; } = "Recent";
    public int NearbyStaleSeconds { get; set; } = 60;
    // Legacy field is consumed once by Normalize when loading an older install.
    public int? PinnedPetId { get; set; }
    public List<int>? PinnedPetIds { get; set; }
    public int PinLimit { get; set; } = 3;

    public IReadOnlyList<int> Pins() => PinnedPetIds ?? (PinnedPetId is int id ? new List<int> { id } : new List<int>());
    public bool IsPinned(int? id) => id is int pet && Pins().Contains(pet);
    public bool CanPin(int id) => IsPinned(id) || Pins().Count < PinLimit;
    void NormalizePins()
    {
        PinLimit = PinLimit is >= 1 and <= 5 ? PinLimit : 3;
        PinnedPetIds = Pins().Where(id => Catalog.Entries.Any(p => p.Id == id)).Distinct().Take(5).ToList();
        PinnedPetId = null;
        // Keep a slot for ordinary activity, even when every allowed pin is used.
        RowLimit = new[] { 3, 5, 8, 10 }.First(n => n >= Math.Max(RowLimit, PinnedPetIds.Count + 1));
    }

    public bool Visible { get; set; } = true;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double? TrackerWidth { get; set; }
    public double? TrackerHeight { get; set; }
    public bool TrackerMaximized { get; set; }
    public bool HideMaxed { get; set; }
    public bool ExpireInactive { get; set; } = true;
    public int InactiveSeconds { get; set; } = 20;
    public bool HideWhenEmpty { get; set; }
    public int RowLimit { get; set; } = 5;
    public double Scale { get; set; } = 1;
    public double Opacity { get; set; } = .95;
    public string Accent { get; set; } = "Gold";
    public string Sort { get; set; } = "Unfinished first";
    public bool Locked { get; set; }
    public bool ClickThrough { get; set; }
    public bool ReducedMotion { get; set; }
    public bool Portraits { get; set; } = true;
    public bool ProgressBars { get; set; } = true;
    public bool Compact { get; set; }
    public bool GameOnly { get; set; }
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool BlackMarketAlerts { get; set; } = true;
    public bool Toasts { get; set; }
    public bool Sounds { get; set; }
    public int Hotkey { get; set; }
    public int HideShortcutKey { get; set; } = 0x44;
    public int HideShortcutModifiers { get; set; } = 1;
    public bool CheckUpdatesAutomatically { get; set; }
    public bool DownloadUpdatesAutomatically { get; set; }
    public string UpdateChannel { get; set; } = "win-stable";
    public string GameProcesses { get; set; } = "AION2,AION2-Win64-Shipping";
    public string? Adapter { get; set; }

    public void Normalize()
    {
        Language = AppLanguage.Normalize(Language);
        CheckUpdatesAutomatically = false;
        DownloadUpdatesAutomatically = false;
        StartWithWindows = false;
        StartMinimized = false;
        ExperimentalDirections = false;
        if (TrackerMode is not ("Recent" or "Nearby" or "Merchant"))
            TrackerMode = "Recent";
        if (!new[] { 15, 30, 60, 120, 300 }.Contains(NearbyStaleSeconds))
            NearbyStaleSeconds = 60;
        if (PinnedPetId is int pin && !Catalog.Entries.Any(p => p.Id == pin))
            PinnedPetId = null;
        if (UpdateChannel is not ("win-stable" or "win-beta"))
            UpdateChannel = "win-stable";
        if (string.IsNullOrWhiteSpace(GameProcesses))
            GameProcesses = "AION2,AION2-Win64-Shipping";
        if (!new[] { "Gold", "Blue", "Teal" }.Contains(Accent))
            Accent = "Gold";
        if (!new[] { "Unfinished first", "Recent only", "Closest to next rank" }.Contains(Sort))
            Sort = "Unfinished first";
        if (!new[] { 0, 0x50, 0x54, 0x4f }.Contains(Hotkey))
            Hotkey = 0;
        if (!new HideShortcut(HideShortcutKey, HideShortcutModifiers).Valid)
        {
            HideShortcutKey = 0x44;
            HideShortcutModifiers = 1;
        }
        if (HideShortcutKey == 0)
            HideShortcutModifiers = 0;
        RowLimit = new[] { 3, 5, 8, 10 }.Contains(RowLimit) ? RowLimit : 5;
        InactiveSeconds = new[] { 15, 20, 30, 60, 120 }.Contains(InactiveSeconds) ? InactiveSeconds : 20;
        Scale = double.IsFinite(Scale) ? Math.Clamp(Scale, .75, 1.5) : 1;
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, .4, 1) : .95;
        if (Left.HasValue && !double.IsFinite(Left.Value))
            Left = null;
        if (Top.HasValue && !double.IsFinite(Top.Value))
            Top = null;
        TrackerWidth = TrackerWidth is { } width && double.IsFinite(width) ? Math.Clamp(width, 240, 8192) : null;
        TrackerHeight = TrackerHeight is { } height && double.IsFinite(height) ? Math.Clamp(height, 120, 8192) : null;
        NormalizePins();
    }
}

public static class SettingsPatch
{
    public static AppSettings Apply(AppSettings current, IReadOnlyDictionary<string, JsonElement> patch)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(current))!.AsObject();
        var allowed = typeof(AppSettings).GetProperties().Where(p => p.CanWrite).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var field in patch)
        {
            if (!allowed.Contains(field.Key))
                throw new InvalidDataException("Unknown setting");
            node[field.Key] = System.Text.Json.Nodes.JsonNode.Parse(field.Value.GetRawText());
        }

        var next = node.Deserialize<AppSettings>() ?? throw new InvalidDataException("Invalid settings");
        next.Normalize();
        return next;
    }

    public static Dictionary<string, JsonElement> Fields(params (string Name, object? Value)[] values) => values.ToDictionary(p => p.Name, p => JsonSerializer.SerializeToElement(p.Value));
}
