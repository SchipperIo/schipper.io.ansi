# Sequences and art

[Index](README.md)

`Ansi` builds escape sequences. `AnsiParser`, `Cp437`, and `Sauce` read classic ANSI art. `Glyphs` holds the box and block characters those screens draw.

## Ansi

Every method appends to a `StringBuilder`, so a frame can be composed and flushed once.

```csharp
using System.Text;
using Schipper.Io.Ansi.Screen;
using Schipper.Io.Ansi.Sequences;

var sb = new StringBuilder();
Ansi.EnableAlternateScreen(sb);
Ansi.HideCursor(sb);
Ansi.BeginSync(sb);
Ansi.ClearScreen(sb);
Ansi.CursorTo(sb, row: 1, column: 1);
Ansi.SetGraphics(sb, cell);
sb.Append("Hello");
Ansi.ResetGraphics(sb);
Ansi.EndSync(sb);
Ansi.ShowCursor(sb);
Ansi.DisableAlternateScreen(sb);
```

| Method | Sequence |
| --- | --- |
| `CursorTo(sb, row, column)` | `CSI row;column H` (1-based) |
| `ClearScreen` | `CSI 2J` |
| `ClearLine` | `CSI K` |
| `HideCursor` / `ShowCursor` | `CSI ?25l` / `CSI ?25h` |
| `EnableAlternateScreen` / `DisableAlternateScreen` | `CSI ?1049h` / `CSI ?1049l` |
| `ResetGraphics` | `CSI 0m` |
| `SetGraphics(sb, cell)` | Reset, then the cell's attributes and colors |
| `BeginSync` / `EndSync` | DEC mode 2026 on / off |

`BeginSync` and `EndSync` wrap a frame so a terminal that implements synchronized output presents it atomically. A terminal that ignores private mode 2026 shows the bytes as they arrive.

`Ansi.Escape` is `ESC`. `Ansi.Csi` is `"\x1b["`.

For a whole screen, prefer `ScreenRenderer`. It coalesces cursor motion and SGR. Use `Ansi` when you are writing a single sequence yourself.

## ANSI art

`AnsiParser.Parse` reads a CP437 `.ans` stream into a `ScreenBuffer`. The default width is 80. Width must be between 1 and 4096 columns; a value outside that range throws `ArgumentOutOfRangeException`. A trailing SAUCE record overrides that width only when `TInfo1` is in `1..4096`. An out-of-range SAUCE width is ignored. The record, its comment block, and the EOF marker are stripped before parsing.

The parser keeps a pending wrap: a glyph in the last column does not start a new row until the next printable character. CR cancels the pending wrap. LF moves down one row and cancels it. TAB (byte `0x09`) advances to the next multiple of 8 without storing a cell; a tab that would pass the last column stays on that column and sets a pending wrap.

Erase in display (`CSI 2J` / `CSI 0J`) and erase in line fill with a space that uses the current SGR pen, not `Cell.Empty`. `CSI 2J` and `CSI 0J` also remember that fill for rows created later (for example after CUP onto a new line). The cursor is not moved.

CSI numeric parameters are clamped to `0..9999`. After CUP/CUU/CUD/CUF/CUB/restore, the cursor is clamped to the buffer width and to 5000 rows. The buffer never grows past 5000 rows.

When a SAUCE record is present and `TFlags` bit 0 is set (iCE / non-blink), SGR 5 selects a bright background instead of `CellAttributes.Blink`. SGR 25 and SGR 0 clear that mode. Backgrounds 40–47 then map to the bright eight while the flag is on; 100–107 stay bright. Without a SAUCE record, or with that bit clear, SGR 5 is blink.

A truncated `38`/`48` color selector (missing mode, unknown mode, or missing index/RGB) is consumed so it is not replayed as bold, faint, or blink. A complete `38;5;n` or `38;2;r;g;b` still leaves later SGR parameters in that same list to run.

```csharp
using Schipper.Io.Ansi.Art;
using Schipper.Io.Ansi.Screen;
using Schipper.Io.Ansi.Sequences;

byte[] file = File.ReadAllBytes("banner.ans");
ScreenBuffer art = AnsiParser.Parse(file);

if (Sauce.TryParse(file, out Sauce? sauce, out int dataLength))
{
    string title = sauce!.Title;
    int columns = sauce.Width;
    int rows = sauce.Height;
    ReadOnlySpan<byte> payload = file.AsSpan(0, dataLength);
}
```

The parser accepts the sequences real BBS art uses: cursor positioning and movement, SGR color and attributes, erase in line and display, and save/restore cursor. The buffer grows with the art, up to 5000 rows.

## Sauce

`Sauce` is the 128-byte metadata record at the end of a file.

| Property | Record field |
| --- | --- |
| `Title`, `Author`, `Group`, `Date` | Text fields |
| `FileSize`, `DataType`, `FileType` | Type bytes |
| `TInfo1`–`TInfo4`, `TFlags` | Type-specific integers |
| `FontName` | Font name |
| `Comments` | Comment-block lines |
| `Width`, `Height` | `TInfo1` and `TInfo2`, for character art |

Text fields (title, author, group, date, font name, and comments) stop at the first NUL byte, then trailing spaces are trimmed.

`TryParse` returns false when the tail is not a SAUCE record. `dataLength` is then the original file length.

## Cp437

`Cp437` maps all 256 bytes to Unicode and back, including the box-drawing and shade characters. It does not use `System.Text.Encoding.CodePages`.

```csharp
char glyph = Cp437.ToChar(0xDB);          // full block
byte b = Cp437.FromChar('█');
string text = Cp437.Decode(artBytes);
byte[] encoded = Cp437.Encode(text);
```

`FromChar` passes characters below U+0080 through unchanged, so ESC, CR, LF, and TAB survive a round trip. Any other character with no CP437 byte becomes `Cp437.Substitute` (`?`).

Bytes in the C0 range decode to the classic CP437 dingbats. If those bytes are control codes in your stream, handle them before calling `Decode`.

## Glyphs

`Glyphs` is a set of `const char` values for the characters you would otherwise look up in a code page:

| Group | Members |
| --- | --- |
| Blocks | `FullBlock`, `DarkShade`, `MediumShade`, `LightShade`, `UpperHalf`, `LowerHalf`, `LeftHalf`, `RightHalf`, `Bullet` |
| Single box | `Horizontal`, `Vertical`, `TopLeft`, `TopRight`, `BottomLeft`, `BottomRight` |
| Double box | `DoubleHorizontal`, `DoubleVertical`, `DoubleTopLeft`, `DoubleTopRight`, `DoubleBottomLeft`, `DoubleBottomRight` |

`Schipper.Io.Tui.Draw` uses these for frames. They are Unicode characters, so they belong in a UTF-8 `ScreenBuffer`, not in a CP437 byte string, until you pass them through `Cp437.Encode`.
