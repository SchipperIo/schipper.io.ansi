using System.Text;
using Schipper.Io.Ansi.Colors;
using Schipper.Io.Ansi.Sequences;

namespace Schipper.Io.Ansi.Screen;

/// <summary>
/// Turns a <see cref="ScreenBuffer"/> into the ANSI byte stream needed to display it, emitting a
/// minimal diff against a previously rendered buffer. Cursor moves and SGR changes are coalesced so
/// only cells that actually changed are repainted. Every method has a <see cref="ColorDepth"/>
/// overload that downgrades cell colors at emit time (before style-run comparison, so two truecolor
/// cells that collapse to the same downgraded color share one SGR run), and a
/// <see cref="StringBuilder"/> overload that renders into a caller-supplied builder so frame
/// builders can be pooled.
/// </summary>
public static class ScreenRenderer
{
    /// <summary>Renders the entire buffer after clearing the screen.</summary>
    public static string RenderFull(ScreenBuffer current) => RenderFull(current, ColorDepth.TrueColor);

    /// <summary>Renders the entire buffer after clearing the screen, downgrading colors to <paramref name="depth"/>.</summary>
    public static string RenderFull(ScreenBuffer current, ColorDepth depth)
    {
        ArgumentNullException.ThrowIfNull(current);
        var sb = new StringBuilder(EstimateCapacity(current));
        AppendFull(sb, current, depth, clearScreen: true);
        return sb.ToString();
    }

    /// <summary>
    /// Renders the entire buffer after clearing the screen into <paramref name="output"/>, which is
    /// cleared first so it can be reused across frames.
    /// </summary>
    public static void RenderFull(ScreenBuffer current, StringBuilder output, ColorDepth depth = ColorDepth.TrueColor)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();
        AppendFull(output, current, depth, clearScreen: true);
    }

    /// <summary>
    /// Renders the whole buffer by rewriting every cell, homing the cursor first but <b>without</b>
    /// clearing the screen. Because every cell is overwritten, the previous frame is fully replaced
    /// with no residue — and, unlike <see cref="RenderFull(ScreenBuffer)"/>, no erase flash. This is
    /// the full-frame path: robust against any drift in what is actually on screen (raw ANSI from a
    /// door, dropped bytes, autowrap quirks) at the cost of sending the entire frame each time.
    /// </summary>
    public static string RenderComplete(ScreenBuffer current) => RenderComplete(current, ColorDepth.TrueColor);

    /// <summary>Renders the whole buffer without clearing, downgrading colors to <paramref name="depth"/>.</summary>
    public static string RenderComplete(ScreenBuffer current, ColorDepth depth)
    {
        ArgumentNullException.ThrowIfNull(current);
        var sb = new StringBuilder(EstimateCapacity(current));
        AppendFull(sb, current, depth, clearScreen: false);
        return sb.ToString();
    }

    /// <summary>
    /// Renders the whole buffer without clearing into <paramref name="output"/>, which is cleared
    /// first so it can be reused across frames.
    /// </summary>
    public static void RenderComplete(ScreenBuffer current, StringBuilder output, ColorDepth depth = ColorDepth.TrueColor)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();
        AppendFull(output, current, depth, clearScreen: false);
    }

    /// <summary>
    /// Renders only the cells that differ from <paramref name="previous"/>. Falls back to a full
    /// render when the dimensions changed or there is no previous frame. Returns an empty string
    /// when nothing changed.
    /// </summary>
    public static string RenderDiff(ScreenBuffer? previous, ScreenBuffer current) =>
        RenderDiff(previous, current, ColorDepth.TrueColor);

    /// <summary>
    /// Renders only the cells that differ from <paramref name="previous"/>, downgrading colors to
    /// <paramref name="depth"/>. Returns an empty string when nothing changed.
    /// </summary>
    public static string RenderDiff(ScreenBuffer? previous, ScreenBuffer current, ColorDepth depth)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (previous is null || previous.Width != current.Width || previous.Height != current.Height)
        {
            return RenderFull(current, depth);
        }

        var sb = new StringBuilder();
        AppendDiff(sb, previous, current, depth);
        return sb.ToString();
    }

    /// <summary>
    /// Renders only the cells that differ from <paramref name="previous"/> into
    /// <paramref name="output"/>, which is cleared first so it can be reused across frames. The
    /// builder is left empty when nothing changed.
    /// </summary>
    public static void RenderDiff(ScreenBuffer? previous, ScreenBuffer current, StringBuilder output, ColorDepth depth = ColorDepth.TrueColor)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(output);
        output.Clear();
        if (previous is null || previous.Width != current.Width || previous.Height != current.Height)
        {
            AppendFull(output, current, depth, clearScreen: true);
            return;
        }

        AppendDiff(output, previous, current, depth);
    }

    /// <summary>
    /// Estimated frame size: one glyph plus amortized cursor-move/SGR overhead per cell, plus the
    /// fixed prologue/epilogue sequences.
    /// </summary>
    private static int EstimateCapacity(ScreenBuffer current) => (current.Width * current.Height * 3) + 32;

    private static void AppendFull(StringBuilder sb, ScreenBuffer current, ColorDepth depth, bool clearScreen)
    {
        if (clearScreen)
        {
            Sequences.Ansi.HideCursor(sb);
            Sequences.Ansi.ResetGraphics(sb);
            Sequences.Ansi.ClearScreen(sb);
        }
        else
        {
            Sequences.Ansi.ResetGraphics(sb);
        }

        Sequences.Ansi.CursorTo(sb, 1, 1);
        AppendChanges(sb, previous: null, current, depth);
        Sequences.Ansi.ResetGraphics(sb);
    }

    private static void AppendDiff(StringBuilder sb, ScreenBuffer previous, ScreenBuffer current, ColorDepth depth)
    {
        if (AppendChanges(sb, previous, current, depth))
        {
            Sequences.Ansi.ResetGraphics(sb);
        }
    }

    private static bool AppendChanges(StringBuilder sb, ScreenBuffer? previous, ScreenBuffer current, ColorDepth depth)
    {
        bool wrote = false;
        bool penValid = false;
        Cell pen = default;

        // Expected terminal cursor position (1-based), -1 meaning "unknown, force a move".
        int curRow = -1;
        int curCol = -1;

        for (int y = 0; y < current.Height; y++)
        {
            for (int x = 0; x < current.Width; x++)
            {
                Cell cell = current[x, y];
                if (previous is not null && cell == previous[x, y])
                {
                    continue;
                }

                // Downgrade before the style-run comparison so two cells whose colors collapse to
                // the same downgraded value share one SGR run instead of emitting redundant SGRs.
                if (depth != ColorDepth.TrueColor)
                {
                    cell = new Cell(cell.Glyph, cell.Foreground.Downgrade(depth), cell.Background.Downgrade(depth), cell.Attributes);
                }

                int row = y + 1;
                int col = x + 1;

                if (curRow != row || curCol != col)
                {
                    Sequences.Ansi.CursorTo(sb, row, col);
                    curRow = row;
                    curCol = col;
                }

                if (!penValid || !cell.HasSameStyle(pen))
                {
                    Sequences.Ansi.SetGraphics(sb, cell);
                    pen = cell;
                    penValid = true;
                }

                sb.Append(cell.Glyph);
                wrote = true;

                // Advance our notion of the cursor. At the right edge the terminal may auto-wrap,
                // so treat the position as unknown and force a move before the next write.
                if (x == current.Width - 1)
                {
                    curRow = -1;
                    curCol = -1;
                }
                else
                {
                    curCol = col + 1;
                }
            }
        }

        return wrote;
    }
}
