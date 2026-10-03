using Schipper.Io.Ansi.Art;
using Schipper.Io.Ansi.Colors;
using Schipper.Io.Ansi.Screen;

namespace Schipper.Io.Ansi.Sequences;

/// <summary>
/// Parses a CP437 ANSI art stream (<c>.ans</c>) into a <see cref="ScreenBuffer"/>. Supports the
/// subset of sequences that real BBS art uses: cursor positioning/movement, SGR colors and
/// attributes, erase-in-line/display, and save/restore cursor. A trailing SAUCE record (and its
/// comment block / EOF marker) is detected and stripped before parsing.
/// </summary>
public static class AnsiParser
{
    private const int DefaultWidth = 80;
    private const int MaxRows = 5000;
    private const int MaxColumns = 4096;
    private const int MaxCsiValue = 9999;

    /// <summary>Parses art bytes into a screen buffer of the given column width.</summary>
    public static ScreenBuffer Parse(ReadOnlySpan<byte> data, int width = DefaultWidth)
    {
        if (width is < 1 or > MaxColumns)
        {
            throw new ArgumentOutOfRangeException(nameof(width), width, $"Width must be between 1 and {MaxColumns}.");
        }

        bool iceColors = false;
        if (Sauce.TryParse(data, out Sauce? sauce, out int dataLength))
        {
            data = data[..dataLength];
            if (sauce!.Width is >= 1 and <= MaxColumns)
            {
                width = sauce.Width;
            }

            iceColors = (sauce.TFlags & 1) != 0;
        }

        List<Cell[]> rows = [];
        int cursorX = 0;
        int cursorY = 0;
        int savedX = 0;
        int savedY = 0;
        int maxY = 0;
        bool wrapPending = false;
        bool brightBg = false;

        Color fg = Color.Default;
        Color bg = Color.Default;
        CellAttributes attrs = CellAttributes.None;
        Cell rowFill = Cell.Empty;

        EnsureRow(rows, 0, width, rowFill);

        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];

            if (b == 0x1A)
            {
                break;
            }

            if (b == Ansi.Escape)
            {
                if (i + 1 >= data.Length || data[i + 1] != '[')
                {
                    continue;
                }

                int j = i + 2;
                while (j < data.Length && !(data[j] is >= 0x40 and <= 0x7E))
                {
                    j++;
                }

                if (j >= data.Length)
                {
                    break;
                }

                ReadOnlySpan<byte> paramBytes = data.Slice(i + 2, j - (i + 2));
                char final = (char)data[j];
                ApplySequence(
                    paramBytes,
                    final,
                    rows,
                    width,
                    ref cursorX,
                    ref cursorY,
                    ref savedX,
                    ref savedY,
                    ref wrapPending,
                    ref fg,
                    ref bg,
                    ref attrs,
                    ref rowFill,
                    iceColors,
                    ref brightBg);
                maxY = Math.Max(maxY, cursorY);
                i = j;
                continue;
            }

            switch (b)
            {
                case 0x0D:
                    cursorX = 0;
                    wrapPending = false;
                    break;

                case 0x0A:
                    cursorY++;
                    wrapPending = false;
                    if (cursorY >= MaxRows)
                    {
                        cursorY = MaxRows - 1;
                    }

                    break;

                case 0x09:
                    wrapPending = false;
                    cursorX = ((cursorX / 8) + 1) * 8;
                    if (cursorX >= width)
                    {
                        cursorX = width - 1;
                        wrapPending = true;
                    }

                    break;

                default:
                    if (wrapPending)
                    {
                        wrapPending = false;
                        cursorX = 0;
                        cursorY++;
                    }

                    if (cursorY >= MaxRows)
                    {
                        break;
                    }

                    EnsureRow(rows, cursorY, width, rowFill);
                    if (cursorX < width)
                    {
                        rows[cursorY][cursorX] = new Cell(Cp437.ToChar(b), fg, bg, attrs);
                    }

                    if (cursorX >= width - 1)
                    {
                        wrapPending = true;
                    }
                    else
                    {
                        cursorX++;
                    }

                    maxY = Math.Max(maxY, cursorY);
                    break;
            }

            maxY = Math.Max(maxY, cursorY);
        }

        int height = Math.Min(Math.Max(maxY + 1, 1), MaxRows);
        ScreenBuffer buffer = new(width, height);
        for (int y = 0; y < height && y < rows.Count; y++)
        {
            Cell[] row = rows[y];
            for (int x = 0; x < width; x++)
            {
                buffer[x, y] = row[x];
            }
        }

        return buffer;
    }

    private static void ApplySequence(
        ReadOnlySpan<byte> paramBytes,
        char final,
        List<Cell[]> rows,
        int width,
        ref int cursorX,
        ref int cursorY,
        ref int savedX,
        ref int savedY,
        ref bool wrapPending,
        ref Color fg,
        ref Color bg,
        ref CellAttributes attrs,
        ref Cell rowFill,
        bool iceColors,
        ref bool brightBg)
    {
        switch (final)
        {
            case 'm':
                ApplySgr(paramBytes, ref fg, ref bg, ref attrs, iceColors, ref brightBg);
                break;

            case 'H':
            case 'f':
            {
                (int row, int col) = ParseTwo(paramBytes, 1, 1);
                cursorY = row - 1;
                cursorX = col - 1;
                wrapPending = false;
                ClampCursor(width, ref cursorX, ref cursorY);
                break;
            }

            case 'A':
                cursorY = (int)Math.Max(0L, (long)cursorY - ParseOne(paramBytes, 1));
                wrapPending = false;
                ClampCursor(width, ref cursorX, ref cursorY);
                break;

            case 'B':
                cursorY = (int)Math.Min(MaxRows - 1L, (long)cursorY + ParseOne(paramBytes, 1));
                wrapPending = false;
                ClampCursor(width, ref cursorX, ref cursorY);
                break;

            case 'C':
                cursorX = (int)Math.Min(width - 1L, (long)cursorX + ParseOne(paramBytes, 1));
                wrapPending = false;
                ClampCursor(width, ref cursorX, ref cursorY);
                break;

            case 'D':
                cursorX = (int)Math.Max(0L, (long)cursorX - ParseOne(paramBytes, 1));
                wrapPending = false;
                ClampCursor(width, ref cursorX, ref cursorY);
                break;

            case 's':
                savedX = cursorX;
                savedY = cursorY;
                break;

            case 'u':
                cursorX = savedX;
                cursorY = savedY;
                wrapPending = false;
                ClampCursor(width, ref cursorX, ref cursorY);
                break;

            case 'J':
                ApplyEraseDisplay(ParseOne(paramBytes, 0), rows, width, cursorX, cursorY, fg, bg, attrs, ref rowFill);
                break;

            case 'K':
                ApplyEraseLine(ParseOne(paramBytes, 0), rows, width, cursorX, cursorY, fg, bg, attrs, rowFill);
                break;
        }
    }

    private static void ClampCursor(int width, ref int cursorX, ref int cursorY)
    {
        if (cursorX < 0)
        {
            cursorX = 0;
        }
        else if (cursorX >= width)
        {
            cursorX = width - 1;
        }

        if (cursorY < 0)
        {
            cursorY = 0;
        }
        else if (cursorY >= MaxRows)
        {
            cursorY = MaxRows - 1;
        }
    }

    private static void ApplySgr(
        ReadOnlySpan<byte> paramBytes,
        ref Color fg,
        ref Color bg,
        ref CellAttributes attrs,
        bool iceColors,
        ref bool brightBg)
    {
        Span<int> values = stackalloc int[16];
        int count = SplitParams(paramBytes, values, defaultWhenEmpty: 0);

        for (int k = 0; k < count; k++)
        {
            int code = values[k];
            switch (code)
            {
                case 0:
                    fg = Color.Default;
                    bg = Color.Default;
                    attrs = CellAttributes.None;
                    brightBg = false;
                    break;
                case 1: attrs |= CellAttributes.Bold; break;
                case 2: attrs |= CellAttributes.Faint; break;
                case 3: attrs |= CellAttributes.Italic; break;
                case 4: attrs |= CellAttributes.Underline; break;
                case 5:
                    if (iceColors)
                    {
                        SetIceBrightBg(ref bg, ref brightBg, enabled: true);
                    }
                    else
                    {
                        attrs |= CellAttributes.Blink;
                    }

                    break;
                case 7: attrs |= CellAttributes.Reverse; break;
                case 8: attrs |= CellAttributes.Hidden; break;
                case 9: attrs |= CellAttributes.Strikethrough; break;
                case 22: attrs &= ~(CellAttributes.Bold | CellAttributes.Faint); break;
                case 23: attrs &= ~CellAttributes.Italic; break;
                case 24: attrs &= ~CellAttributes.Underline; break;
                case 25:
                    if (iceColors)
                    {
                        SetIceBrightBg(ref bg, ref brightBg, enabled: false);
                    }
                    else
                    {
                        attrs &= ~CellAttributes.Blink;
                    }

                    break;
                case 27: attrs &= ~CellAttributes.Reverse; break;
                case 28: attrs &= ~CellAttributes.Hidden; break;
                case 29: attrs &= ~CellAttributes.Strikethrough; break;
                case 39: fg = Color.Default; break;
                case 49: bg = Color.Default; break;
                case 38:
                    fg = ParseExtendedColor(values, count, ref k) ?? fg;
                    break;
                case 48:
                    bg = ParseExtendedColor(values, count, ref k) ?? bg;
                    break;
                default:
                    if (code is >= 30 and <= 37)
                    {
                        fg = Color.Basic((BasicColor)(code - 30));
                    }
                    else if (code is >= 40 and <= 47)
                    {
                        int index = code - 40;
                        if (iceColors && brightBg)
                        {
                            index += 8;
                        }

                        bg = Color.Basic((BasicColor)index);
                    }
                    else if (code is >= 90 and <= 97)
                    {
                        fg = Color.Basic((BasicColor)(code - 90 + 8));
                    }
                    else if (code is >= 100 and <= 107)
                    {
                        bg = Color.Basic((BasicColor)(code - 100 + 8));
                    }

                    break;
            }
        }
    }

    private static void SetIceBrightBg(ref Color bg, ref bool brightBg, bool enabled)
    {
        if (enabled == brightBg)
        {
            return;
        }

        brightBg = enabled;
        if (bg.Kind != ColorKind.Basic)
        {
            return;
        }

        int index = (int)bg.AsBasic;
        if (enabled && index is >= 0 and <= 7)
        {
            bg = Color.Basic((BasicColor)(index + 8));
        }
        else if (!enabled && index is >= 8 and <= 15)
        {
            bg = Color.Basic((BasicColor)(index - 8));
        }
    }

    private static Color? ParseExtendedColor(ReadOnlySpan<int> values, int count, ref int k)
    {
        // Caller is positioned on the 38/48 selector; the mode follows.
        if (k + 1 >= count)
        {
            k = count - 1;
            return null;
        }

        int mode = values[k + 1];
        if (mode == 5 && k + 2 < count)
        {
            byte idx = (byte)Math.Clamp(values[k + 2], 0, 255);
            k += 2;
            return Color.Palette(idx);
        }

        if (mode == 2 && k + 4 < count)
        {
            byte r = (byte)Math.Clamp(values[k + 2], 0, 255);
            byte g = (byte)Math.Clamp(values[k + 3], 0, 255);
            byte bl = (byte)Math.Clamp(values[k + 4], 0, 255);
            k += 4;
            return Color.Rgb(r, g, bl);
        }

        k = count - 1;
        return null;
    }

    private static void ApplyEraseDisplay(
        int mode,
        List<Cell[]> rows,
        int width,
        int cursorX,
        int cursorY,
        Color fg,
        Color bg,
        CellAttributes attrs,
        ref Cell rowFill)
    {
        Cell fill = new(' ', fg, bg, attrs);
        switch (mode)
        {
            case 2:
                foreach (Cell[] row in rows)
                {
                    Array.Fill(row, fill);
                }

                rowFill = fill;
                break;
            case 0:
                EnsureRow(rows, cursorY, width, rowFill);
                for (int x = cursorX; x < width; x++)
                {
                    rows[cursorY][x] = fill;
                }

                for (int y = cursorY + 1; y < rows.Count; y++)
                {
                    Array.Fill(rows[y], fill);
                }

                rowFill = fill;
                break;
            case 1:
                for (int y = 0; y < cursorY && y < rows.Count; y++)
                {
                    Array.Fill(rows[y], fill);
                }

                EnsureRow(rows, cursorY, width, rowFill);
                for (int x = 0; x <= cursorX && x < width; x++)
                {
                    rows[cursorY][x] = fill;
                }

                break;
        }
    }

    private static void ApplyEraseLine(
        int mode,
        List<Cell[]> rows,
        int width,
        int cursorX,
        int cursorY,
        Color fg,
        Color bg,
        CellAttributes attrs,
        Cell rowFill)
    {
        EnsureRow(rows, cursorY, width, rowFill);
        Cell[] line = rows[cursorY];
        Cell fill = new(' ', fg, bg, attrs);
        switch (mode)
        {
            case 0:
                for (int x = cursorX; x < width; x++)
                {
                    line[x] = fill;
                }

                break;
            case 1:
                for (int x = 0; x <= cursorX && x < width; x++)
                {
                    line[x] = fill;
                }

                break;
            case 2:
                Array.Fill(line, fill);
                break;
        }
    }

    private static void EnsureRow(List<Cell[]> rows, int y, int width, Cell rowFill)
    {
        if ((uint)y >= (uint)MaxRows)
        {
            return;
        }

        while (rows.Count <= y)
        {
            Cell[] row = new Cell[width];
            Array.Fill(row, rowFill);
            rows.Add(row);
        }
    }

    private static int SplitParams(ReadOnlySpan<byte> paramBytes, Span<int> output, int defaultWhenEmpty)
    {
        if (paramBytes.IsEmpty)
        {
            output[0] = defaultWhenEmpty;
            return 1;
        }

        int count = 0;
        long value = 0;
        bool any = false;
        foreach (byte b in paramBytes)
        {
            if (b is >= (byte)'0' and <= (byte)'9')
            {
                value = (value * 10) + (b - '0');
                if (value > MaxCsiValue)
                {
                    value = MaxCsiValue;
                }

                any = true;
            }
            else if (b == (byte)';')
            {
                if (count < output.Length)
                {
                    output[count++] = any ? (int)value : 0;
                }

                value = 0;
                any = false;
            }
        }

        if (count < output.Length)
        {
            output[count++] = any ? (int)value : 0;
        }

        return count;
    }

    private static int ParseOne(ReadOnlySpan<byte> paramBytes, int fallback)
    {
        Span<int> values = stackalloc int[4];
        int count = SplitParams(paramBytes, values, fallback);
        int v = count > 0 ? values[0] : fallback;
        return v == 0 && fallback != 0 ? fallback : v;
    }

    private static (int, int) ParseTwo(ReadOnlySpan<byte> paramBytes, int fallback1, int fallback2)
    {
        Span<int> values = stackalloc int[4];
        int count = SplitParams(paramBytes, values, 0);
        int a = count > 0 && values[0] > 0 ? values[0] : fallback1;
        int b = count > 1 && values[1] > 0 ? values[1] : fallback2;
        return (a, b);
    }
}
