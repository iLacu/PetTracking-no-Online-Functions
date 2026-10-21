using System.Globalization;

namespace PetTracker;

public sealed record LanguageOption(string Code, string Label)
{
    public override string ToString() => Label;
}

public static class AppLanguage
{
    public static IReadOnlyList<LanguageOption> Options { get; } = new[]
    {
        new LanguageOption("auto", "System language (automatic)"),
        new LanguageOption("de", "Deutsch"), new LanguageOption("en", "English"),
        new LanguageOption("es", "Español"), new LanguageOption("fr", "Français"),
        new LanguageOption("it", "Italiano"), new LanguageOption("pl", "Polski")
    };

    public static string Normalize(string? language)
    {
        string value = language?.Trim().ToLowerInvariant() ?? "auto";
        return Options.Any(o => o.Code == value) ? value : "auto";
    }

    public static string Resolve(string? choice, CultureInfo systemCulture)
    {
        string selected = Normalize(choice);
        if (selected != "auto") return selected;
        string system = systemCulture.TwoLetterISOLanguageName;
        return Options.Any(o => o.Code == system) ? system : "en";
    }
}
