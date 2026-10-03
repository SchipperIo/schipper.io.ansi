# Schipper.Io.Ansi

Zero-dependency ANSI and terminal primitives for .NET 10. The package owns color, the cell grid, diff rendering, keyboard decoding, CP437, SAUCE, and the `ITerminal` byte channel that a host fills in.

`Schipper.Io.Tui` is the widget layer on top of this package. `Schipper.Io.Xterm` is a separate toolkit and does not reference this one.

## Guides

| Guide | What it covers |
| --- | --- |
| [Getting started](getting-started.md) | Package reference, namespaces, and a first frame |
| [Color](color.md) | `Color`, `BasicColor`, `ColorDepth`, and `Downgrade` |
| [Screen](screen.md) | `Cell`, `ScreenBuffer`, and `ScreenRenderer` |
| [Input and the terminal](input.md) | `ITerminal`, `InputParser`, and `KeyEvent` |
| [Sequences and art](sequences-and-art.md) | `Ansi`, `AnsiParser`, `Cp437`, `Sauce`, and `Glyphs` |

## Namespaces

| Namespace | Types |
| --- | --- |
| `Schipper.Io.Ansi` | `ITerminal`, `TerminalExtensions`, `TerminalEncoding` |
| `Schipper.Io.Ansi.Colors` | `Color`, `ColorKind`, `BasicColor`, `ColorDepth` |
| `Schipper.Io.Ansi.Screen` | `Cell`, `CellAttributes`, `ScreenBuffer`, `ScreenRenderer` |
| `Schipper.Io.Ansi.Input` | `InputParser`, `Key`, `KeyEvent` |
| `Schipper.Io.Ansi.Sequences` | `Ansi`, `AnsiParser` |
| `Schipper.Io.Ansi.Art` | `Cp437`, `Sauce`, `Glyphs` |

The wire encoding is UTF-8. `TerminalEncoding.Cp437` is a handshake hint a host may store. CP437 bytes are the on-disk ANSI-art format, decoded with `Cp437`.

## Package

```xml
<PackageReference Include="Schipper.Io.Ansi" Version="0.1.0-dev" />
```

Target framework: `net10.0`. The package has no dependencies.
