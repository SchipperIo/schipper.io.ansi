# Color

[Index](README.md)

`Schipper.Io.Ansi.Colors.Color` is one value with four representations. Construct it with the factory that matches the representation you mean.

| Factory | Kind | Meaning |
| --- | --- | --- |
| `Color.Default` | `ColorKind.Default` | The terminal's default foreground or background (SGR 39 / 49) |
| `Color.Basic(BasicColor.Cyan)` | `ColorKind.Basic` | One of the 16 ANSI colors |
| `Color.Palette(196)` | `ColorKind.Palette256` | An index in the xterm 256-color palette |
| `Color.Rgb(222, 238, 240)` | `ColorKind.TrueColor` | 24-bit RGB |

`Kind` tells you which payload is live. `AsBasic` reads a basic color. `PaletteIndex` reads a 256-color index. `R`, `G`, and `B` read a truecolor value. Those readers match the kind you stored.

`BasicColor` is the classic set: `Black` through `White`, then `BrightBlack` through `BrightWhite`.

## Color depth

`ColorDepth` is the capability of the remote terminal:

| Value | Sequences |
| --- | --- |
| `Basic16` | SGR 30–37 / 90–97 and 40–47 / 100–107 |
| `Palette256` | SGR `38;5;n` and `48;5;n` |
| `TrueColor` | SGR `38;2;r;g;b` and `48;2;r;g;b` |

Pass the remote's depth into `ScreenRenderer`. The renderer downgrades each cell before it compares style runs, so two truecolor cells that collapse to the same palette index share one SGR span.

```csharp
Color ink = Color.Rgb(40, 150, 160);
Color forClient = ink.Downgrade(ColorDepth.Palette256);
string frame = ScreenRenderer.RenderDiff(previous, current, ColorDepth.Palette256);
```

`Downgrade` leaves a color alone when it already fits the target depth. `Color.Default` passes through at every depth. Truecolor mapped to `Palette256` picks the nearest xterm-256 cube or gray-ramp entry. Anything mapped to `Basic16` picks the nearest of the 16 standard xterm basic colors from a precomputed table.

## Emitting SGR yourself

`AppendSgr` writes the SGR parameters for one color. `foreground: true` selects the foreground form.

```csharp
var sb = new StringBuilder();
sb.Append(Ansi.Csi);
ink.AppendSgr(sb, foreground: true);
sb.Append('m');
```

`Ansi.SetGraphics` is the usual path. It resets the pen, then applies the attributes and both colors of a `Cell`.

## Equality

`Color` is a readonly struct. Equality compares kind and the stored bytes. Use it as a dictionary key or in a `Cell` comparison.
