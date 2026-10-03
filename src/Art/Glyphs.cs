namespace Schipper.Io.Ansi.Art;

/// <summary>
/// CP437 block-drawing and box glyphs for door art. Authors get authentic BBS visuals without
/// memorizing code points; the host re-encodes these to CP437 or UTF-8 to match each terminal.
/// </summary>
public static class Glyphs
{
    public const char FullBlock = '█';      // █
    public const char DarkShade = '▓';      // ▓
    public const char MediumShade = '▒';    // ▒
    public const char LightShade = '░';     // ░
    public const char UpperHalf = '▀';      // ▀
    public const char LowerHalf = '▄';      // ▄
    public const char LeftHalf = '▌';       // ▌
    public const char RightHalf = '▐';      // ▐
    public const char Bullet = '•';         // •

    // Single-line box drawing.
    public const char Horizontal = '─';     // ─
    public const char Vertical = '│';       // │
    public const char TopLeft = '┌';        // ┌
    public const char TopRight = '┐';       // ┐
    public const char BottomLeft = '└';     // └
    public const char BottomRight = '┘';    // ┘

    // Double-line box drawing.
    public const char DoubleHorizontal = '═';   // ═
    public const char DoubleVertical = '║';     // ║
    public const char DoubleTopLeft = '╔';      // ╔
    public const char DoubleTopRight = '╗';     // ╗
    public const char DoubleBottomLeft = '╚';   // ╚
    public const char DoubleBottomRight = '╝';  // ╝
}
