# Input and the terminal

[Index](README.md)

`ITerminal` is the byte pipe. `InputParser` turns those bytes into `KeyEvent` values.

## ITerminal

```csharp
namespace Schipper.Io.Ansi;

public interface ITerminal
{
    int Columns { get; }
    int Rows { get; }
    event Action? Resized;
    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
```

`Columns` and `Rows` are character cells. Raise `Resized` when the remote window changes. `ReadAsync` returns `0` at the end of the stream.

`TerminalExtensions` adds UTF-8 writes:

```csharp
await terminal.WriteAsync("\x1b[2J\x1b[H");
await terminal.WriteAsync(frameBuilder); // encodes a StringBuilder without an intermediate string
byte[] utf8 = terminal.Encode("text");
```

The `StringBuilder` overload rents one buffer from the array pool, encodes every chunk with a single UTF-8 encoder so a surrogate pair on a chunk boundary stays intact, and returns the buffer after the write.

This package does not open a console or an SSH channel. The host implements `ITerminal`. `Schipper.Io.Xterm.ConsoleTerminal` is one implementation, in the Xterm package, and it is a different `ITerminal` type.

`TerminalEncoding` is `Cp437` or `Utf8`. It is a value a handshake can carry. Bytes you `WriteAsync` are the bytes on the wire. Treat them as UTF-8.

## InputParser

```csharp
using System.Text;
using Schipper.Io.Ansi.Input;

var parser = new InputParser();
var kept = new List<KeyEvent>();

int read = await terminal.ReadAsync(buffer);
parser.Feed(buffer.AsSpan(0, read), kept);

foreach (KeyEvent ev in kept)
{
    if (ev.Key == Key.Enter)
    {
        break;
    }

    if (ev.IsChar && !ev.Ctrl)
    {
        text.Append(ev.Char);
    }
}
```

The parser accepts control characters, CSI and SS3 sequences for arrows, function keys, and navigation, and Alt-prefixed keys. Ordinary keystrokes are UTF-8: a multi-byte scalar (including one split across reads) is one `Key.Char`. An invalid sequence or a supplementary-plane scalar becomes U+FFFD. ESC followed by a UTF-8 scalar, rather than `[` or `O`, is Alt+that character once the scalar is complete. CP437 is not used on this path.

A sequence split across reads stays in the parser until it completes. Inside CSI, only `0x20..0x3F` are parameter bytes and `0x40..0x7E` is the final. A new ESC, a C0 control, DEL, or a byte `>= 0x80` abandons the unfinished CSI so the interrupting byte can be parsed. A CSI longer than 64 bytes without a final is dropped. SS3 consumes a third byte only when that byte is a final `0x40..0x7E`. `PendingCount` is the number of buffered bytes. `PendingIsLoneEscape` is true when the only buffered byte is ESC.

CR at the end of a feed is held. The next LF collapses to one Enter; any other following byte is Enter plus that byte. `Flush` emits Enter for a held CR.

`Feed(ReadOnlySpan<byte>)` returns an internal list. That list is valid until the next call to the same overload. Copy the events, or use `Feed(bytes, List<KeyEvent>)`, which appends to a list you own and does not clear it first.

`Flush` resolves a lone ESC as `Key.Escape`, a held CR as Enter, and clears any partial buffer. Call it when input has gone idle and a lone ESC can no longer be the start of a sequence. `Schipper.Io.Tui.InputReader` does that when a read delivers a lone ESC and no event was decoded.

## KeyEvent

| Member | Meaning |
| --- | --- |
| `Key` | `Char`, `Enter`, `Escape`, `Backspace`, `Tab`, arrows, `Home`, `End`, `PageUp`, `PageDown`, `Insert`, `Delete`, `F1`–`F12`, or `None` |
| `Char` | The character when `Key` is `Char` |
| `Ctrl`, `Alt`, `Shift` | Modifiers |
| `IsChar` | `Key == Key.Char` |
| `KeyEvent.FromChar(c)` | A character event, with an optional Alt flag |

`Key.None` is produced internally and is not added to the event list.
