using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Data;
using Wpf = System.Windows.Controls;

namespace PetTracker;

public static class UiLanguage
{
    static readonly CultureInfo systemCulture = CultureInfo.CurrentUICulture;
    static readonly Dictionary<string, string[]> catalog = ReadCatalog();
    static readonly string[] codes = ["de", "es", "fr", "it", "pl"];
    static readonly List<(Regex Pattern, string[] Values)> templates = catalog.Where(p => p.Key.Contains("{0}")).OrderByDescending(p => p.Key.Length).Select(p =>
    {
        string pattern = Regex.Escape(p.Key);
        for (int i = 0; i < 8; i++) pattern = pattern.Replace(Regex.Escape("{" + i + "}"), "(?<p" + i + ">.+?)");
        return (new Regex("\\A" + pattern + "\\z", RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(30)), p.Value);
    }).ToList();
    static readonly List<(WeakReference<DependencyObject> Target, DependencyProperty[] Properties)> controls = new();
    public static string Current { get; private set; } = "en";
    public static event Action? Changed;
    public static IReadOnlyDictionary<string, string[]> Catalog => catalog;

    static Dictionary<string, string[]> ReadCatalog()
    {
        using var resource = typeof(UiLanguage).Assembly.GetManifestResourceStream("PetTracker.Translations.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(resource)!;
    }

    public static void Initialize(string dataRoot)
    {
        string choice = "auto";
        try
        {
            string file = Path.Combine(dataRoot, "settings.json");
            if (File.Exists(file) && new FileInfo(file).Length <= 1024 * 1024)
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var document = JsonDocument.Parse(stream);
                if (document.RootElement.TryGetProperty(nameof(AppSettings.Language), out var field) && field.ValueKind == JsonValueKind.String)
                    choice = field.GetString() ?? "auto";
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { }
        Use(choice);
    }

    public static void Use(string? choice)
    {
        string next = AppLanguage.Resolve(choice, systemCulture);
        if (next == Current) return;
        Current = next;
        for (int i = controls.Count - 1; i >= 0; i--)
        {
            if (!controls[i].Target.TryGetTarget(out var control)) { controls.RemoveAt(i); continue; }
            foreach (var property in controls[i].Properties) control.CoerceValue(property);
        }
        Changed?.Invoke();
    }

    public static string Text(string? source) => Translate(source ?? "", 0);
    static string Translate(string source, int depth)
    {
        if (source.Length == 0 || depth > 3) return source;
        // Several prerequisite messages were originally written in German.
        source = EnglishAliases.Source(source);
        if (Current == "en") return source;
        int index = Array.IndexOf(codes, Current);
        if (index < 0) return source;
        if (catalog.TryGetValue(source, out var exact)) return exact[index];
        if (source.Contains('\n')) return string.Join("\n", source.Split('\n').Select(part => Translate(part, depth + 1)));
        foreach (var template in templates)
        {
            var match = template.Pattern.Match(source);
            if (!match.Success) continue;
            string result = template.Values[index];
            for (int i = 0; i < 8; i++)
            {
                var group = match.Groups["p" + i];
                if (group.Success) result = result.Replace("{" + i + "}", group.Value == source ? group.Value : Translate(group.Value, depth + 1));
            }
            return result;
        }
        foreach (string separator in new[] { "\n", " · " })
            if (source.Contains(separator)) return string.Join(separator, source.Split(separator).Select(part => Translate(part, depth + 1)));
        if (source.StartsWith("✓ ") || source.StartsWith("! ")) return source[..2] + Translate(source[2..], depth + 1);
        return source;
    }

    internal static object Coerce(DependencyObject owner, object value) => value is string text ? Text(text) : value;
    internal static void Track(DependencyObject control, params DependencyProperty[] properties)
    {
        if (controls.Count % 128 == 0) controls.RemoveAll(entry => !entry.Target.TryGetTarget(out _));
        controls.Add((new(control), properties));
    }

    public static void Attach(Window window)
    {
        string title = window.Title;
        bool writing = false;
        void RefreshTitle() { writing = true; try { window.Title = Text(title); } finally { writing = false; } }
        var descriptor = DependencyPropertyDescriptor.FromProperty(Window.TitleProperty, window.GetType());
        EventHandler update = (_, _) => { if (!writing) { title = window.Title; RefreshTitle(); } };
        descriptor.AddValueChanged(window, update);
        Changed += RefreshTitle;
        window.Closed += (_, _) => { Changed -= RefreshTitle; descriptor.RemoveValueChanged(window, update); };
        RefreshTitle();
    }

    internal static DataTemplate ItemTemplate(string member = "")
    {
        var text = new FrameworkElementFactory(typeof(LocalizedTextBlock));
        text.SetBinding(System.Windows.Controls.TextBlock.TextProperty, new Binding(member));
        text.SetValue(System.Windows.Controls.TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        return new DataTemplate { VisualTree = text };
    }
}

public class LocalizedTextBlock : Wpf.TextBlock
{
    static LocalizedTextBlock() => TextProperty.OverrideMetadata(typeof(LocalizedTextBlock), new FrameworkPropertyMetadata("", null, UiLanguage.Coerce));
    public LocalizedTextBlock() => UiLanguage.Track(this, TextProperty);
}
public class LocalizedButton : Wpf.Button
{
    static LocalizedButton() => ContentProperty.OverrideMetadata(typeof(LocalizedButton), new FrameworkPropertyMetadata(null, null, UiLanguage.Coerce));
    public LocalizedButton() { UiLanguage.Track(this, ContentProperty); SetResourceReference(StyleProperty, typeof(Wpf.Button)); }
}
public class LocalizedTextBox : Wpf.TextBox
{
    static LocalizedTextBox() => TagProperty.OverrideMetadata(typeof(LocalizedTextBox), new FrameworkPropertyMetadata(null, null, UiLanguage.Coerce));
    public LocalizedTextBox() { UiLanguage.Track(this, TagProperty); SetResourceReference(StyleProperty, typeof(Wpf.TextBox)); }
}
public sealed class LocalizedStringTemplateSelector : DataTemplateSelector
{
    readonly DataTemplate template = UiLanguage.ItemTemplate();
    public override DataTemplate? SelectTemplate(object item, DependencyObject container) => item is string ? template : null;
}
public class LocalizedCheckBox : Wpf.CheckBox
{
    static LocalizedCheckBox() => ContentProperty.OverrideMetadata(typeof(LocalizedCheckBox), new FrameworkPropertyMetadata(null, null, UiLanguage.Coerce));
    public LocalizedCheckBox() { UiLanguage.Track(this, ContentProperty); SetResourceReference(StyleProperty, typeof(Wpf.CheckBox)); }
}
public class LocalizedExpander : Wpf.Expander
{
    static LocalizedExpander() => HeaderProperty.OverrideMetadata(typeof(LocalizedExpander), new FrameworkPropertyMetadata(null, null, UiLanguage.Coerce));
    public LocalizedExpander() { UiLanguage.Track(this, HeaderProperty); Foreground = Ui.Ink; }
}
public class LocalizedComboBox : Wpf.ComboBox
{
    public LocalizedComboBox() => SetResourceReference(StyleProperty, typeof(Wpf.ComboBox));
    protected override void OnInitialized(EventArgs e)
    {
        string member = DisplayMemberPath;
        DisplayMemberPath = "";
        ItemTemplate ??= UiLanguage.ItemTemplate(member);
        base.OnInitialized(e);
    }
}
public class LocalizedToolTip : Wpf.ToolTip
{
    static LocalizedToolTip() => ContentProperty.OverrideMetadata(typeof(LocalizedToolTip), new FrameworkPropertyMetadata(null, null, UiLanguage.Coerce));
    public LocalizedToolTip() { UiLanguage.Track(this, ContentProperty); SetResourceReference(StyleProperty, typeof(Wpf.ToolTip)); }
}
public class LocalizedMenuItem : Wpf.MenuItem
{
    static LocalizedMenuItem() => HeaderProperty.OverrideMetadata(typeof(LocalizedMenuItem), new FrameworkPropertyMetadata(null, null, UiLanguage.Coerce));
    public LocalizedMenuItem() => UiLanguage.Track(this, HeaderProperty);
}

static class EnglishAliases
{
    static readonly Dictionary<string, string> values = Read();
    static readonly List<(Regex Pattern, string Value)> templates = values.Where(p => p.Key.Contains("{0}")).Select(p =>
    {
        string pattern = Regex.Escape(p.Key);
        for (int i = 0; i < 8; i++) pattern = pattern.Replace(Regex.Escape("{" + i + "}"), "(?<p" + i + ">.+?)");
        return (new Regex("\\A" + pattern + "\\z", RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(30)), p.Value);
    }).ToList();
    static Dictionary<string, string> Read()
    {
        using var stream = typeof(UiLanguage).Assembly.GetManifestResourceStream("PetTracker.TranslationAliases.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
    public static string Source(string value)
    {
        if (values.TryGetValue(value, out var exact)) return exact;
        foreach (var template in templates)
        {
            var match = template.Pattern.Match(value);
            if (!match.Success) continue;
            string result = template.Value;
            for (int i = 0; i < 8; i++) if (match.Groups["p" + i] is { Success: true } group) result = result.Replace("{" + i + "}", group.Value);
            return result;
        }
        if (value.StartsWith("✓ ") || value.StartsWith("! ")) return value[..2] + Source(value[2..]);
        return value;
    }
}
