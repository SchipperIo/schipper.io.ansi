namespace Schipper.Io.Ansi.Screen;

/// <summary>Character rendition flags mapped to SGR attribute codes.</summary>
[Flags]
public enum CellAttributes : byte
{
    None = 0,
    Bold = 1 << 0,
    Faint = 1 << 1,
    Italic = 1 << 2,
    Underline = 1 << 3,
    Blink = 1 << 4,
    Reverse = 1 << 5,
    Hidden = 1 << 6,
    Strikethrough = 1 << 7,
}
