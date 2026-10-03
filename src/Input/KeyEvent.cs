namespace Schipper.Io.Ansi.Input;

/// <summary>Logical key identities produced by <see cref="InputParser"/>.</summary>
public enum Key
{
    None,
    Char,
    Enter,
    Escape,
    Backspace,
    Tab,
    Up,
    Down,
    Left,
    Right,
    Home,
    End,
    PageUp,
    PageDown,
    Insert,
    Delete,
    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,
}

/// <summary>A decoded key press: a logical key plus, for <see cref="Key.Char"/>, the character.</summary>
public readonly struct KeyEvent
{
    public KeyEvent(Key key, char character = '\0', bool ctrl = false, bool alt = false, bool shift = false)
    {
        Key = key;
        Char = character;
        Ctrl = ctrl;
        Alt = alt;
        Shift = shift;
    }

    public Key Key { get; }
    public char Char { get; }
    public bool Ctrl { get; }
    public bool Alt { get; }
    public bool Shift { get; }

    public static KeyEvent FromChar(char c, bool alt = false) => new(Key.Char, c, alt: alt);

    public bool IsChar => Key == Key.Char;

    public override string ToString() =>
        Key == Key.Char ? $"Char '{Char}'{(Alt ? " +Alt" : string.Empty)}" : Key.ToString();
}
