# Screen

[Index](README.md)

A screen is a grid of `Cell` values. Paint the grid, then ask `ScreenRenderer` for the ANSI that shows it.

## Cell

```csharp
using Schipper.Io.Ansi.Colors;
using Schipper.Io.Ansi.Screen;

var cell = new Cell('A', Color.Basic(BasicColor.White), Color.Basic(BasicColor.Blue), CellAttributes.Bold);
Cell blank = Cell.Empty; // space, default colors, no attributes
Cell swapped = cell.WithGlyph('B');
```

`HasSameStyle` compares foreground, background, and attributes, and ignores the glyph. The renderer uses that to coalesce a run of cells into one SGR sequence.

`CellAttributes` is a flags enum: `Bold`, `Faint`, `Italic`, `Underline`, `Blink`, `Reverse`, `Hidden`, `Strikethrough`. Combine them with `|`.

## ScreenBuffer

```csharp
var screen = new ScreenBuffer(80, 24);

screen.Set(0, 0, 'X', Color.Basic(BasicColor.Green), Color.Default);
screen.Set(1, 0, cell);
screen.DrawText(0, 1, "hello", Color.Default, Color.Default);
screen.DrawHorizontalLine(0, 2, 10, '─', Color.Basic(BasicColor.Cyan), Color.Default);
screen.DrawVerticalLine(0, 0, 5, '│', Color.Basic(BasicColor.Cyan), Color.Default);

screen.Fill(Cell.Empty);
screen.Clear(); // Fill(Cell.Empty)

if (screen.InBounds(x, y))
{
    Cell at = screen[x, y];
}

screen.Resize(100, 30); // keeps the overlapping rectangle, clears the new area
ScreenBuffer copy = screen.Clone();
screen.Blit(copy, destX: 10, destY: 2);
```

`Width` and `Height` are the current size. Both the constructor and `Resize` throw `ArgumentOutOfRangeException` when a dimension is not positive or when `width * height` overflows an `int`. `Cells` is the row-major backing store. The indexer throws when the point is outside the buffer. `Set` drops an out-of-range point. `DrawText` stops at the right edge.

`Blit` copies every cell of the source. A source cell that lands outside the destination is dropped by `Set`.

## ScreenRenderer

Three ways to turn a buffer into text:

| Method | What it emits |
| --- | --- |
| `RenderFull` | Clears the screen, then paints every cell |
| `RenderComplete` | Homes the cursor and rewrites every cell, with no erase |
| `RenderDiff` | Cursor moves and SGR only for cells that differ from `previous` |

Each method has a `string` return and a `StringBuilder` overload. The builder overload clears the builder first, so one builder can be reused every frame. Every method takes a `ColorDepth`. The default is `TrueColor`.

```csharp
using System.Text;
using Schipper.Io.Ansi.Colors;
using Schipper.Io.Ansi.Screen;

var output = new StringBuilder();
ScreenBuffer? previous = null;

void Paint(ScreenBuffer current, ColorDepth depth)
{
    ScreenRenderer.RenderDiff(previous, current, output, depth);
    previous = current.Clone();
}
```

`RenderDiff` returns an empty string when nothing changed. It falls back to a clearing full render when `previous` is null or the size differs.

`RenderFull` is the first frame: the clear removes whatever was on the terminal. `RenderComplete` is the drift repair: every cell is rewritten in place, so a byte that was dropped earlier cannot leave a stale glyph behind, and the screen does not flash an erase.

Keep the previous buffer you actually sent. If something else writes to the terminal between flushes, the next diff is computed against a screen the terminal no longer shows. Call `RenderComplete` for that frame, then resume diffing from the buffer you just sent.

`Schipper.Io.Tui.TerminalView` already does this: first paint is `RenderFull`, later paints are `RenderDiff`, and a full rewrite runs after `Invalidate`, after a resize, and every `FullRepaintInterval` diffs.
