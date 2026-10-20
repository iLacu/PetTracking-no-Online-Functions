namespace PetTracker;

public readonly record struct HideShortcut(int Key, int Modifiers)
{
    public static HideShortcut From(AppSettings settings) => new(settings.HideShortcutKey, settings.HideShortcutModifiers);
    public bool Valid => Key == 0 || (Modifiers > 0 && (Modifiers & ~15) == 0 && SupportedKey(Key));
    public static bool SupportedKey(int key) => key is >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5a or >= 0x70 and <= 0x7b or 0x20 or 0x2d or 0x2e or 0x24 or 0x23 or 0x21 or 0x22;
    public override string ToString()
    {
        if (Key == 0) return "Disabled";
        string key = Key switch
        {
            >= 0x70 and <= 0x7b => "F" + (Key - 0x6f),
            0x20 => "Space", 0x2d => "Insert", 0x2e => "Delete", 0x24 => "Home", 0x23 => "End", 0x21 => "Page Up", 0x22 => "Page Down",
            _ => ((char)Key).ToString()
        };
        return ((Modifiers & 2) != 0 ? "Ctrl + " : "") + ((Modifiers & 1) != 0 ? "Alt + " : "") + ((Modifiers & 4) != 0 ? "Shift + " : "") + ((Modifiers & 8) != 0 ? "Win + " : "") + key;
    }
}
