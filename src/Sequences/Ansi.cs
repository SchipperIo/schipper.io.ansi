using System.Text;
using Schipper.Io.Ansi.Screen;

namespace Schipper.Io.Ansi.Sequences;

/// <summary>
/// Low-level helpers for building ANSI/VT escape sequences. All methods append to a
/// <see cref="StringBuilder"/> so callers can compose a whole frame before flushing.
/// </summary>
public static class Ansi
{
    public const char Escape = '\x1b';
    public const string Csi = "\x1b[";

    /// <summary>Moves the cursor to a 1-based row/column.</summary>
    public static StringBuilder CursorTo(StringBuilder sb, int row, int column) =>
        sb.Append(Csi).Append(row).Append(';').Append(column).Append('H');

    public static StringBuilder ClearScreen(StringBuilder sb) => sb.Append(Csi).Append("2J");

    public static StringBuilder ClearLine(StringBuilder sb) => sb.Append(Csi).Append('K');

    public static StringBuilder HideCursor(StringBuilder sb) => sb.Append(Csi).Append("?25l");

    public static StringBuilder ShowCursor(StringBuilder sb) => sb.Append(Csi).Append("?25h");

    public static StringBuilder EnableAlternateScreen(StringBuilder sb) => sb.Append(Csi).Append("?1049h");

    public static StringBuilder DisableAlternateScreen(StringBuilder sb) => sb.Append(Csi).Append("?1049l");

    public static StringBuilder ResetGraphics(StringBuilder sb) => sb.Append(Csi).Append("0m");

    /// <summary>
    /// Begins a synchronized output block (DEC private mode 2026). Terminals that support it hold
    /// screen updates until <see cref="EndSync"/> so a frame is presented atomically, eliminating
    /// tearing; terminals that don't simply ignore the sequence.
    /// </summary>
    public static StringBuilder BeginSync(StringBuilder sb) => sb.Append(Csi).Append("?2026h");

    /// <summary>Ends a synchronized output block started by <see cref="BeginSync"/>.</summary>
    public static StringBuilder EndSync(StringBuilder sb) => sb.Append(Csi).Append("?2026l");

    /// <summary>
    /// Emits a full SGR sequence that resets, then applies the attributes and colors of <paramref name="cell"/>.
    /// Reset-based output keeps the stream robust against a desynced pen at the cost of a few extra bytes.
    /// </summary>
    public static StringBuilder SetGraphics(StringBuilder sb, in Cell cell)
    {
        sb.Append(Csi).Append('0');

        CellAttributes a = cell.Attributes;
        if ((a & CellAttributes.Bold) != 0) sb.Append(";1");
        if ((a & CellAttributes.Faint) != 0) sb.Append(";2");
        if ((a & CellAttributes.Italic) != 0) sb.Append(";3");
        if ((a & CellAttributes.Underline) != 0) sb.Append(";4");
        if ((a & CellAttributes.Blink) != 0) sb.Append(";5");
        if ((a & CellAttributes.Reverse) != 0) sb.Append(";7");
        if ((a & CellAttributes.Hidden) != 0) sb.Append(";8");
        if ((a & CellAttributes.Strikethrough) != 0) sb.Append(";9");

        sb.Append(';');
        cell.Foreground.AppendSgr(sb, foreground: true);
        sb.Append(';');
        cell.Background.AppendSgr(sb, foreground: false);

        sb.Append('m');
        return sb;
    }
}
