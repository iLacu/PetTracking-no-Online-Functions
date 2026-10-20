using System.Windows.Threading;

namespace PetTracker;

public sealed class MerchantPanel : Border, IDisposable
{
    readonly Host host;
    readonly Func<DateTimeOffset> clock;
    readonly PetNames names = new();
    readonly PetPortraits portraits;
    readonly TextBlock countdown = Ui.Text("", 21), refreshTime = Ui.Text("", 10, Ui.Muted), observed = Ui.Text("", 10, Ui.Muted);
    readonly StackPanel offers = new();
    readonly Dictionary<int, Button> tabs = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    int selectedTab = 101;
    string rowsKey = "";
    bool disposed;
    public int OfferCount { get; private set; }
    public MerchantPanel(Host host, Func<DateTimeOffset> clock)
    {
        this.host = host;
        this.clock = clock;
        portraits = new(host.AssetRoot);
        Padding = new Thickness(4, 10, 4, 5);
        var content = new StackPanel();
        Child = content;
        content.Children.Add(Ui.Text("Black Cloud Merchants", 13));
        countdown.Margin = new Thickness(0, 7, 0, 2);
        countdown.FontWeight = FontWeights.SemiBold;
        System.Windows.Automation.AutomationProperties.SetAutomationId(countdown, "MerchantCountdown");
        content.Children.Add(countdown);
        content.Children.Add(refreshTime);
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 6) };
        foreach (int id in new[] { 101, 104 })
        {
            var button = Ui.Button(id == 101 ? "Special" : "Pets", () => { selectedTab = id; Refresh(); });
            button.FontSize = 11;
            button.Padding = new Thickness(14, 4, 14, 4);
            System.Windows.Automation.AutomationProperties.SetAutomationId(button, "MerchantTab-" + id);
            tabs[id] = button;
            bar.Children.Add(button);
        }
        content.Children.Add(bar);
        observed.Margin = new Thickness(0, 0, 0, 7);
        content.Children.Add(observed);
        content.Children.Add(new ScrollViewer { Content = offers, MaxHeight = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        var help = Ui.Text("Open Special and Pets in the game to read each tab. Offers are last observed; purchases or manual refreshes can change them.", 10, Ui.Muted);
        help.Margin = new Thickness(0, 9, 0, 0);
        content.Children.Add(help);
        timer.Tick += (s, e) => Refresh();
        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible && !disposed) { Refresh(); timer.Start(); }
            else timer.Stop();
        };
    }
    public void Refresh()
    {
        if (disposed) return;
        var now = clock();
        var view = host.View;
        bool connected = view?.Ready == true && !string.IsNullOrEmpty(view.Selected);
        var stocks = connected ? (view!.MerchantStock ?? view.Market ?? Array.Empty<MarketStock>()).Where(s => s.Tab is 101 or 104 && now >= s.SeenUtc && now < s.RefreshUtc).ToArray() : Array.Empty<MarketStock>();
        var stock = stocks.FirstOrDefault(s => s.Tab == selectedTab);
        var expiry = stock?.RefreshUtc ?? stocks.OrderBy(s => s.RefreshUtc).FirstOrDefault()?.RefreshUtc;
        if (expiry is { } at)
        {
            var left = TimeSpan.FromSeconds(Math.Max(0, Math.Ceiling((at - now).TotalSeconds)));
            countdown.Text = "Refresh in " + (left.TotalHours >= 1 ? left.ToString(@"hh\:mm\:ss") : left.ToString(@"mm\:ss"));
            refreshTime.Text = "Next refresh · " + at.ToLocalTime().ToString("HH:mm:ss");
        }
        else
        {
            countdown.Text = "Refresh — : —";
            refreshTime.Text = "Open the merchant in game to read the refresh time.";
        }
        countdown.Foreground = NativeTheme.Accent(host.PetOverlayPreferences.Accent);
        foreach (var tab in tabs)
        {
            bool selected = tab.Key == selectedTab;
            tab.Value.Background = selected ? NativeTheme.Accent(host.PetOverlayPreferences.Accent) : Brushes.Transparent;
            tab.Value.Foreground = selected ? Brushes.Black : Ui.Muted;
        }
        observed.Text = stock == null ? "No current observation for " + (selectedTab == 101 ? "Special" : "Pets") + "." : "Last observed · " + stock.SeenUtc.ToLocalTime().ToString("HH:mm:ss") + " · " + stock.Offers.Count + " pets";
        OfferCount = stock?.Offers.Count ?? 0;
        string key = $"{view?.Selected}/{view?.Character}/{view?.WorkerProcessId}/{connected}/{view?.CollectionSynced}/{selectedTab}/{stock?.SeenUtc:O}/{stock?.RefreshUtc:O}/{host.PetOverlayPreferences.Portraits}/" + string.Join("|", (stock?.Offers ?? Array.Empty<MarketOffer>()).Select(o => $"{o.OfferId}:{o.PetId}:{o.Price}:{view!.Profile.Pets.GetValueOrDefault(o.PetId)?.Level}:{view.Profile.Pets.GetValueOrDefault(o.PetId)?.LevelVerified}"));
        if (key == rowsKey) return;
        rowsKey = key;
        offers.Children.Clear();
        if (stock == null)
            offers.Children.Add(Ui.Text(connected ? "Open this tab in the game. Expired offers are cleared at refresh." : "Waiting for a character connection. Then open the merchant in game.", 12, Ui.Muted));
        else if (stock.Offers.Count == 0)
            offers.Children.Add(Ui.Text("No pet offers were found in this observed tab.", 12, Ui.Muted));
        else foreach (var offer in stock.Offers)
        {
            var pet = view!.Profile.Pets.GetValueOrDefault(offer.PetId) ?? Catalog.Create()[offer.PetId];
            var row = new Grid { Margin = new Thickness(0, 9, 0, 9) };
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new());
            if (host.PetOverlayPreferences.Portraits)
                row.Children.Add(new Image { Source = portraits.Get(pet.Id), Width = 44, Height = 44, Margin = new Thickness(0, 0, 9, 0), Stretch = Stretch.Uniform });
            var text = new StackPanel();
            Grid.SetColumn(text, 1);
            row.Children.Add(text);
            var name = Ui.Text(names.Resolve(pet), 12);
            name.FontWeight = FontWeights.SemiBold;
            text.Children.Add(name);
            var rank = Ui.Text(pet.LevelVerified && pet.Level is { } level ? (level == 0 ? "Unranked" : "Rank " + level) + (view.CollectionSynced ? "" : " · last known") : "Rank ? · collection not synced", 10, Ui.Muted);
            rank.Margin = new Thickness(0, 3, 0, 2);
            text.Children.Add(rank);
            text.Children.Add(Ui.Text(offer.Price.ToString("N0") + " Kina", 11));
            System.Windows.Automation.AutomationProperties.SetAutomationId(row, "MerchantOffer-" + selectedTab + "-" + offer.OfferId);
            offers.Children.Add(new Border { Child = row, BorderBrush = new SolidColorBrush(Color.FromRgb(41, 47, 56)), BorderThickness = new Thickness(0, 0, 0, 1) });
        }
    }
    public void Dispose() { disposed = true; timer.Stop(); }
}
