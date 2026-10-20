namespace PetTracker;

// Last observed shop contents, kept separately from the short-lived purchase
// recommendations. These are observations, never a promise of remaining stock.
public sealed class MerchantStockBook
{
    readonly Dictionary<int, MarketStock> tabs = new();
    public void Clear() => tabs.Clear();
    public void Observe(MarketStock stock)
    {
        if (stock.Tab is not (101 or 104) || stock.RefreshUtc <= stock.SeenUtc) return;
        foreach (int tab in tabs.Where(p => p.Value.RefreshUtc != stock.RefreshUtc).Select(p => p.Key).ToArray())
            tabs.Remove(tab);
        tabs[stock.Tab] = stock;
    }
    public IReadOnlyList<MarketStock> Read(DateTimeOffset now) => tabs.Values.Where(s => now >= s.SeenUtc && now < s.RefreshUtc).OrderBy(s => s.Tab).ToArray();
}
