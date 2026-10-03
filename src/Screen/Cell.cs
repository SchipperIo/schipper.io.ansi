using Schipper.Io.Ansi.Colors;

namespace Schipper.Io.Ansi.Screen;

/// <summary>
/// A single character cell: a glyph plus foreground/background colors and rendition attributes.
/// Immutable value type so screen buffers can be diffed cheaply.
/// </summary>
public readonly struct Cell : IEquatable<Cell>
{
    public Cell(char glyph, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        Glyph = glyph;
        Foreground = foreground;
        Background = background;
        Attributes = attributes;
    }

    public char Glyph { get; }
    public Color Foreground { get; }
    public Color Background { get; }
    public CellAttributes Attributes { get; }

    /// <summary>A blank cell using the terminal default colors.</summary>
    public static Cell Empty => new(' ', Color.Default, Color.Default, CellAttributes.None);

    /// <summary>Returns a copy of this cell with a different glyph but the same styling.</summary>
    public Cell WithGlyph(char glyph) => new(glyph, Foreground, Background, Attributes);

    /// <summary>True when two cells differ only in glyph, not in styling.</summary>
    public bool HasSameStyle(in Cell other) =>
        Foreground == other.Foreground &&
        Background == other.Background &&
        Attributes == other.Attributes;

    public bool Equals(Cell other) => Glyph == other.Glyph && HasSameStyle(other);

    public override bool Equals(object? obj) => obj is Cell c && Equals(c);

    public override int GetHashCode() => HashCode.Combine(Glyph, Foreground, Background, (byte)Attributes);

    public static bool operator ==(Cell left, Cell right) => left.Equals(right);

    public static bool operator !=(Cell left, Cell right) => !left.Equals(right);
}
