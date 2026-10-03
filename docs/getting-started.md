# Getting started

[Index](README.md)

Add the package and target `net10.0`:

```xml
<PackageReference Include="Schipper.Io.Ansi" Version="0.1.0-dev" />
```

A frame is a `ScreenBuffer`. You paint cells, then `ScreenRenderer` turns the buffer into ANSI text. A host that implements `ITerminal` writes those bytes.

```csharp
using Schipper.Io.Ansi;
using Schipper.Io.Ansi.Art;
using Schipper.Io.Ansi.Colors;
using Schipper.Io.Ansi.Screen;

var screen = new ScreenBuffer(width: 80, height: 24);
screen.Clear();

var ink = Color.Rgb(222, 238, 240);
var paper = Color.Rgb(8, 20, 34);
screen.Fill(new Cell(' ', ink, paper));
screen.DrawText(2, 1, "Schipper.Io.Ansi", Color.Basic(BasicColor.BrightCyan), paper, CellAttributes.Bold);
screen.DrawHorizontalLine(2, 2, 16, Glyphs.Horizontal, Color.Basic(BasicColor.Cyan), paper);

string frame = ScreenRenderer.RenderFull(screen, ColorDepth.TrueColor);
await terminal.WriteAsync(frame);
```

`terminal` is any `ITerminal`. `WriteAsync(string)` encodes UTF-8. ANSI bytes are ASCII, so they pass through that encoding unchanged.

## Coordinates

`ScreenBuffer` cells are zero-based. `x` grows to the right and `y` grows downward. `Set` ignores a point outside the buffer. `DrawText` clips at the right edge.

`Ansi.CursorTo` is one-based, matching CSI cursor addressing. `ScreenRenderer` applies that conversion when it emits cursor moves. Callers that paint with `ScreenBuffer` stay in zero-based cells.

## What belongs in this package

Color, cells, the screen buffer, diff rendering, input decoding, CP437, and SAUCE live here. Menus, themes, and modal prompts live in `Schipper.Io.Tui`. Mouse, bracketed paste, OSC hyperlinks, and a local `ConsoleTerminal` live in `Schipper.Io.Xterm`.

## Next

- [Color](color.md) for depth and downgrade.
- [Screen](screen.md) for diffing frames.
- [Input and the terminal](input.md) for `ITerminal` and keys.
- [Sequences and art](sequences-and-art.md) for raw CSI and `.ans` files.
