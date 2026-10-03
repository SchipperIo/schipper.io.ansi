using Schipper.Io.Ansi.Colors;

namespace Schipper.Io.Ansi.Screen;

/// <summary>
/// A fixed-size grid of <see cref="Cell"/>s with simple drawing primitives. Coordinates are
/// zero-based with (0,0) at the top-left. Drawing operations clip silently to the bounds.
/// </summary>
public sealed class ScreenBuffer
{
    private Cell[] _cells;

    public ScreenBuffer(int width, int height)
    {
        int count = CheckedCellCount(width, height);
        Width = width;
        Height = height;
        _cells = new Cell[count];
        Clear();
    }

    public int Width { get; private set; }

    public int Height { get; private set; }

    public Cell this[int x, int y]
    {
        get => _cells[Index(x, y)];
        set => _cells[Index(x, y)] = value;
    }

    /// <summary>Direct read-only view of the backing store, row-major.</summary>
    public ReadOnlySpan<Cell> Cells => _cells;

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    /// <summary>Fills the whole buffer with a blank cell using default colors.</summary>
    public void Clear() => Fill(Cell.Empty);

    /// <summary>Fills the whole buffer with the given cell.</summary>
    public void Fill(in Cell cell)
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            _cells[i] = cell;
        }
    }

    /// <summary>Resizes the buffer, preserving overlapping content and clearing new area.</summary>
    public void Resize(int width, int height)
    {
        int count = CheckedCellCount(width, height);
        if (width == Width && height == Height)
        {
            return;
        }

        Cell[] next = new Cell[count];
        for (int i = 0; i < next.Length; i++)
        {
            next[i] = Cell.Empty;
        }

        int copyW = Math.Min(width, Width);
        int copyH = Math.Min(height, Height);
        for (int y = 0; y < copyH; y++)
        {
            for (int x = 0; x < copyW; x++)
            {
                next[(y * width) + x] = _cells[(y * Width) + x];
            }
        }

        _cells = next;
        Width = width;
        Height = height;
    }

    public void Set(int x, int y, char glyph, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        if (InBounds(x, y))
        {
            _cells[Index(x, y)] = new Cell(glyph, foreground, background, attributes);
        }
    }

    public void Set(int x, int y, in Cell cell)
    {
        if (InBounds(x, y))
        {
            _cells[Index(x, y)] = cell;
        }
    }

    /// <summary>Writes a string left-to-right starting at (x,y), clipping at the right edge.</summary>
    public void DrawText(int x, int y, string text, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        if (text.Length == 0 || (uint)y >= (uint)Height)
        {
            return;
        }

        for (int i = 0; i < text.Length; i++)
        {
            int cx = x + i;
            if ((uint)cx >= (uint)Width)
            {
                if (cx >= Width)
                {
                    break;
                }

                continue;
            }

            _cells[Index(cx, y)] = new Cell(text[i], foreground, background, attributes);
        }
    }

    /// <summary>Draws a horizontal run of one glyph.</summary>
    public void DrawHorizontalLine(int x, int y, int length, char glyph, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        for (int i = 0; i < length; i++)
        {
            Set(x + i, y, glyph, foreground, background, attributes);
        }
    }

    /// <summary>Draws a vertical run of one glyph.</summary>
    public void DrawVerticalLine(int x, int y, int length, char glyph, Color foreground, Color background, CellAttributes attributes = CellAttributes.None)
    {
        for (int i = 0; i < length; i++)
        {
            Set(x, y + i, glyph, foreground, background, attributes);
        }
    }

    /// <summary>Copies another buffer onto this one at the given offset (used for compositing/blits).</summary>
    public void Blit(ScreenBuffer source, int destX, int destY)
    {
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                Set(destX + x, destY + y, source[x, y]);
            }
        }
    }

    /// <summary>Creates an independent copy of this buffer.</summary>
    public ScreenBuffer Clone()
    {
        var clone = new ScreenBuffer(Width, Height);
        _cells.AsSpan().CopyTo(clone._cells);
        return clone;
    }

    private int Index(int x, int y)
    {
        if (!InBounds(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is outside {Width}x{Height}.");
        }

        return (y * Width) + x;
    }

    private static int CheckedCellCount(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        long product = (long)width * height;
        if (product > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width * height overflows an array length.");
        }

        return (int)product;
    }
}
