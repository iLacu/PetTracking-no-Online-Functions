namespace PetTracker;

public sealed class PetNames
{
    public string Resolve(PetProgress p) => !string.IsNullOrEmpty(p.DisplayName) && p.DisplayName != p.CatalogName ? p.DisplayName : Catalog.Entries.FirstOrDefault(c => c.Id == p.Id)?.Name ?? "Pet #" + p.Id;

    public string CreatureSubtitle(PetProgress pet, string? observedNames)
    {
        static string Normalize(string value) => string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        string petName = Normalize(Resolve(pet));
        // Nearby groups the actually observed source mobs into a comma-separated list.
        // Do not substitute other possible sources from the catalog for a missing sighting.
        var different = (observedNames ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(Normalize)
            .Where(name => name.Length > 0 && !name.StartsWith("NPC #", StringComparison.OrdinalIgnoreCase) && !name.Equals(petName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        string label = string.Join(", ", different);
        return label.Length == 0 ? "" : "(" + label + ")";
    }
}
