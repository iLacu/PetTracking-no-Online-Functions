using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PetTracker;

static class MobNameChecks
{
    static IEnumerable<DependencyObject> Children(DependencyObject root)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Children(child)) yield return nested;
        }
    }

    public static void Run(Action<bool, string> check, string folder, Action<Window> show, Action<int> pump)
    {
        using var host = new Host(Path.Combine(folder, "mob-name-fixture"), false);
        var profile = new Profile { CharacterName = "Fixture", Pets = Catalog.Create() };
        foreach (int id in new[] { 1046, 1128, 1148 })
        {
            profile.Pets[id].Level = 1;
            profile.Pets[id].Souls = 12;
            profile.Pets[id].Goal = 25;
        }
        string sameName = new PetNames().Resolve(profile.Pets[1128]);
        var prefs = new AppSettings { ReducedMotion = true, ExpireInactive = false, RowLimit = 5 };
        var now = DateTimeOffset.UtcNow;
        var view = new WorkerView(3, profile, true, "Simulation", "Artificial mob observations", "Fixture", new(), "0123456789abcdef01234567",
            new() { new(1046, 2400002, "Red Shell Mole", now), new(1128, null, sameName, now), new(1148, null, "Crash Site Kalgolem", now) },
            null, now, 1, 0, 0, 0, prefs);
        host.Preview(view);
        var overlay = host.Overlay!;
        show(overlay);
        pump(30);
        TextBlock Label(int id) => Children(overlay).OfType<TextBlock>().Single(t => System.Windows.Automation.AutomationProperties.GetAutomationId(t) == "PetMobName-" + id);
        ContentControl Row(int id) => Children(overlay).OfType<ContentControl>().Single(t => System.Windows.Automation.AutomationProperties.GetAutomationId(t) == "PetRow-" + id);
        check(Label(1046).Text == "(Red Shell Mole)" && Label(1046).IsVisible, "Armadon shows the observed Red Shell Mole beneath its pet name");
        check(Label(1148).Text == "(Crash Site Kalgolem)" && Label(1148).IsVisible, "Mob subtitles also apply to a different pet and creature family");
        check(Label(1128).Visibility == Visibility.Collapsed && Row(1128).Height == PetHudActivityPolicy.RowHeight, "Matching pet and mob names add neither duplicate text nor blank space");
        var armadon = Row(1046);
        check(armadon.Height == PetHudActivityPolicy.RowHeight + 16, "A differing mob name receives enough row height for rank and progress");
        foreach (int id in new[] { 1046, 1148 })
        {
            var row = Row(id);
            check(Children(row).OfType<TextBlock>().Where(t => t.IsVisible).All(t => t.TranslatePoint(new Point(0, 0), row).Y >= 0 && t.TranslatePoint(new Point(0, t.ActualHeight), row).Y <= row.ActualHeight), "Mob subtitle, rank and progress stay inside row " + id);
        }
        Program.Save(overlay, Path.Combine(folder, "mob-names-recent.png"), 340, 370);
        host.Preview(view with { Recent = new() { new(1046, 2400002, "Armadon", now) } });
        check(ReferenceEquals(armadon, Row(1046)) && Label(1046).Visibility == Visibility.Collapsed && armadon.Height == PetHudActivityPolicy.RowHeight, "Live name changes reuse the row and restore its original height");
        host.Preview(view with { Recent = new() { new(1046, null, "", now) } });
        check(Label(1046).Visibility == Visibility.Collapsed, "Missing observed names never guess a catalog source");

        prefs.TrackerMode = "Nearby";
        host.Preview(view with { Nearby = new[] { new NearbyPet(1046, "Armadon, Red Shell Mole, Red Shell Mole, Withered Armadon", now), new NearbyPet(1128, "  " + sameName.ToUpperInvariant() + "  ", now) } });
        pump(30);
        check(Label(1046).Text == "(Red Shell Mole, Withered Armadon)", "Nearby lists distinct observed variants and omits the matching pet name");
        check(Label(1128).Visibility == Visibility.Collapsed, "Capitalization and surrounding spaces do not invent a name difference");
        Program.Save(overlay, Path.Combine(folder, "mob-names-nearby.png"), 340, 290);

        prefs.PinnedPetIds = new() { 1046 };
        host.Preview(view with { Nearby = Array.Empty<NearbyPet>() });
        check(Label(1046).Visibility == Visibility.Collapsed, "A pin without a current sighting does not retain an unrelated mob label");
        prefs.PinnedPetIds.Clear();
        prefs.TrackerMode = "Recent";
        prefs.ReducedMotion = false;
        prefs.ExpireInactive = true;
        prefs.InactiveSeconds = 15;
        host.Preview(view with { Recent = new() { new(1046, 2400002, "Red Shell Mole", DateTimeOffset.UtcNow.AddSeconds(-14.2)) } });
        pump(20);
        check(Row(1046).HasAnimatedProperties && Row(1046).Height > PetHudActivityPolicy.RowHeight, "Rows with a mob subtitle retain the normal expiry animation");
        pump(1250);
        check(overlay.DisplayedPetCount == 0, "The taller mob subtitle row expires completely without stale text");
    }
}
