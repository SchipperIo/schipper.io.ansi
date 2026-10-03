using Schipper.Io.Ansi.Sequences;

namespace Schipper.Io.Ansi.Input;

/// <summary>
/// Stateful decoder that turns incoming terminal bytes into <see cref="KeyEvent"/>s. Handles control
/// characters, CSI/SS3 escape sequences for arrows/function/navigation keys, and Alt-prefixed keys,
/// buffering partial escape sequences that arrive split across reads. Buffered bytes live in a
/// head-indexed array so consuming decoded sequences never shifts the remainder.
/// </summary>
public sealed class InputParser
{
    private const int MaxCsiBytes = 64;

    private byte[] _pending = new byte[64];
    private int _start;
    private int _count;
    private readonly List<KeyEvent> _events = [];

    /// <summary>Number of bytes buffered awaiting a complete sequence.</summary>
    public int PendingCount => _count;

    /// <summary>True when the only buffered byte is a lone ESC (the Escape key, not a sequence start).</summary>
    public bool PendingIsLoneEscape => _count == 1 && _pending[_start] == Sequences.Ansi.Escape;

    /// <summary>
    /// Feeds raw bytes and returns the key events that could be fully decoded. The returned list is
    /// an internal buffer reused by this parser: it is valid only until the next call to
    /// <see cref="Feed(ReadOnlySpan{byte})"/>, so consume (or copy) it before feeding again. Use
    /// <see cref="Feed(ReadOnlySpan{byte}, List{KeyEvent})"/> to decode into a caller-owned list.
    /// </summary>
    public IReadOnlyList<KeyEvent> Feed(ReadOnlySpan<byte> bytes)
    {
        _events.Clear();
        Feed(bytes, _events);
        return _events;
    }

    /// <summary>
    /// Feeds raw bytes and appends the key events that could be fully decoded to
    /// <paramref name="events"/> (which is not cleared first).
    /// </summary>
    public void Feed(ReadOnlySpan<byte> bytes, List<KeyEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        Append(bytes);

        while (_count > 0)
        {
            ReadOnlySpan<byte> span = _pending.AsSpan(_start, _count);
            if (!TryParse(span, out int consumed, out KeyEvent ev, out bool incomplete))
            {
                _ = incomplete; // either incomplete (wait) or nothing to do; stop in both cases.
                break;
            }

            Advance(consumed);
            if (ev.Key != Key.None)
            {
                events.Add(ev);
            }
        }
    }

    /// <summary>
    /// Resolves a buffered lone ESC as an Escape press, or a held CR as Enter, and clears any
    /// partial buffer. Call this when input has gone idle.
    /// </summary>
    public KeyEvent? Flush()
    {
        KeyEvent? result = null;
        if (_count == 1 && _pending[_start] == 0x0D)
        {
            result = new KeyEvent(Key.Enter);
        }
        else if (_count >= 1 && _pending[_start] == Sequences.Ansi.Escape)
        {
            result = new KeyEvent(Key.Escape);
        }

        _start = 0;
        _count = 0;
        return result;
    }

    private void Append(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return;
        }

        int required = _count + bytes.Length;
        if (_start + required > _pending.Length)
        {
            if (required <= _pending.Length)
            {
                // Enough total room — compact the live bytes back to the front.
                Array.Copy(_pending, _start, _pending, 0, _count);
            }
            else
            {
                byte[] next = new byte[Math.Max(_pending.Length * 2, required)];
                Array.Copy(_pending, _start, next, 0, _count);
                _pending = next;
            }

            _start = 0;
        }

        bytes.CopyTo(_pending.AsSpan(_start + _count));
        _count += bytes.Length;
    }

    private void Advance(int consumed)
    {
        _start += consumed;
        _count -= consumed;
        if (_count == 0)
        {
            // Empty: reset the head so the whole array is writable again without compaction.
            _start = 0;
        }
    }

    private static bool TryParse(ReadOnlySpan<byte> buf, out int consumed, out KeyEvent ev, out bool incomplete)
    {
        consumed = 0;
        ev = default;
        incomplete = false;

        if (buf.Length == 0)
        {
            return false;
        }

        byte b = buf[0];
        if (b != Sequences.Ansi.Escape)
        {
            if (b == 0x0D)
            {
                if (buf.Length == 1)
                {
                    incomplete = true;
                    return false;
                }

                if (buf[1] == 0x0A)
                {
                    consumed = 2;
                    ev = new KeyEvent(Key.Enter);
                    return true;
                }

                consumed = 1;
                ev = new KeyEvent(Key.Enter);
                return true;
            }

            if (b < 0x80)
            {
                consumed = 1;
                ev = DecodeSingle(b);
                return true;
            }

            if (!TryDecodeUtf8(buf, out consumed, out char ch, out incomplete))
            {
                return false;
            }

            ev = KeyEvent.FromChar(ch);
            return true;
        }

        if (buf.Length == 1)
        {
            incomplete = true;
            return false;
        }

        byte b1 = buf[1];
        if (b1 == 'O')
        {
            if (buf.Length < 3)
            {
                incomplete = true;
                return false;
            }

            byte third = buf[2];
            if (third is >= 0x40 and <= 0x7E)
            {
                consumed = 3;
                ev = MapSs3((char)third);
                return true;
            }

            consumed = 2;
            ev = new KeyEvent(Key.None);
            return true;
        }

        if (b1 == '[')
        {
            for (int i = 2; i < buf.Length; i++)
            {
                if (i >= MaxCsiBytes)
                {
                    consumed = MaxCsiBytes;
                    ev = new KeyEvent(Key.None);
                    return true;
                }

                byte c = buf[i];
                if (c is >= 0x40 and <= 0x7E)
                {
                    consumed = i + 1;
                    ev = MapCsi(buf.Slice(2, i - 2), (char)c);
                    return true;
                }

                if (c is >= 0x20 and <= 0x3F)
                {
                    continue;
                }

                // ESC, CAN, SUB, other C0, DEL, or a high byte: abandon the CSI.
                if (c is 0x0D or 0x0A)
                {
                    consumed = i + 1;
                    if (c == 0x0D && i + 1 < buf.Length && buf[i + 1] == 0x0A)
                    {
                        consumed = i + 2;
                    }

                    ev = new KeyEvent(Key.Enter);
                    return true;
                }

                consumed = i;
                ev = MapCsi(buf.Slice(2, i - 2), '~');
                return true;
            }

            incomplete = true;
            return false;
        }

        if (b1 < 0x80)
        {
            if (b1 is >= 0x20 and < 0x7F)
            {
                consumed = 2;
                ev = KeyEvent.FromChar((char)b1, alt: true);
                return true;
            }

            consumed = 1;
            ev = new KeyEvent(Key.Escape);
            return true;
        }

        if (!TryDecodeUtf8(buf[1..], out int utfConsumed, out char altChar, out incomplete))
        {
            return false;
        }

        consumed = 1 + utfConsumed;
        ev = KeyEvent.FromChar(altChar, alt: true);
        return true;
    }

    private static bool TryDecodeUtf8(ReadOnlySpan<byte> buf, out int consumed, out char ch, out bool incomplete)
    {
        consumed = 0;
        ch = default;
        incomplete = false;

        byte lead = buf[0];
        int needed;
        uint codepoint;
        if ((lead & 0xE0) == 0xC0)
        {
            needed = 2;
            codepoint = (uint)(lead & 0x1F);
        }
        else if ((lead & 0xF0) == 0xE0)
        {
            needed = 3;
            codepoint = (uint)(lead & 0x0F);
        }
        else if ((lead & 0xF8) == 0xF0)
        {
            needed = 4;
            codepoint = (uint)(lead & 0x07);
        }
        else
        {
            consumed = 1;
            ch = '\uFFFD';
            return true;
        }

        if (buf.Length < needed)
        {
            incomplete = true;
            return false;
        }

        for (int i = 1; i < needed; i++)
        {
            byte cont = buf[i];
            if ((cont & 0xC0) != 0x80)
            {
                consumed = 1;
                ch = '\uFFFD';
                return true;
            }

            codepoint = (codepoint << 6) | (uint)(cont & 0x3F);
        }

        uint min = needed switch
        {
            2 => 0x80u,
            3 => 0x800u,
            _ => 0x10000u,
        };

        if (codepoint < min || codepoint > 0x10FFFF || codepoint is >= 0xD800 and <= 0xDFFF || codepoint > 0xFFFF)
        {
            consumed = needed;
            ch = '\uFFFD';
            return true;
        }

        consumed = needed;
        ch = (char)codepoint;
        return true;
    }

    private static KeyEvent DecodeSingle(byte b) => b switch
    {
        0x0D or 0x0A => new KeyEvent(Key.Enter),
        0x09 => new KeyEvent(Key.Tab),
        0x7F or 0x08 => new KeyEvent(Key.Backspace),
        >= 1 and <= 26 => new KeyEvent(Key.Char, (char)('a' + (b - 1)), ctrl: true),
        < 0x20 => new KeyEvent(Key.None),
        _ => KeyEvent.FromChar((char)b),
    };

    private static KeyEvent MapSs3(char final) => final switch
    {
        'A' => new KeyEvent(Key.Up),
        'B' => new KeyEvent(Key.Down),
        'C' => new KeyEvent(Key.Right),
        'D' => new KeyEvent(Key.Left),
        'H' => new KeyEvent(Key.Home),
        'F' => new KeyEvent(Key.End),
        'P' => new KeyEvent(Key.F1),
        'Q' => new KeyEvent(Key.F2),
        'R' => new KeyEvent(Key.F3),
        'S' => new KeyEvent(Key.F4),
        _ => new KeyEvent(Key.None),
    };

    private static KeyEvent MapCsi(ReadOnlySpan<byte> parameters, char final)
    {
        switch (final)
        {
            case 'A': return new KeyEvent(Key.Up);
            case 'B': return new KeyEvent(Key.Down);
            case 'C': return new KeyEvent(Key.Right);
            case 'D': return new KeyEvent(Key.Left);
            case 'H': return new KeyEvent(Key.Home);
            case 'F': return new KeyEvent(Key.End);
            case 'P': return new KeyEvent(Key.F1);
            case 'Q': return new KeyEvent(Key.F2);
            case 'R': return new KeyEvent(Key.F3);
            case 'S': return new KeyEvent(Key.F4);
            case '~':
                return ParseLeadingInt(parameters) switch
                {
                    1 or 7 => new KeyEvent(Key.Home),
                    2 => new KeyEvent(Key.Insert),
                    3 => new KeyEvent(Key.Delete),
                    4 or 8 => new KeyEvent(Key.End),
                    5 => new KeyEvent(Key.PageUp),
                    6 => new KeyEvent(Key.PageDown),
                    11 => new KeyEvent(Key.F1),
                    12 => new KeyEvent(Key.F2),
                    13 => new KeyEvent(Key.F3),
                    14 => new KeyEvent(Key.F4),
                    15 => new KeyEvent(Key.F5),
                    17 => new KeyEvent(Key.F6),
                    18 => new KeyEvent(Key.F7),
                    19 => new KeyEvent(Key.F8),
                    20 => new KeyEvent(Key.F9),
                    21 => new KeyEvent(Key.F10),
                    23 => new KeyEvent(Key.F11),
                    24 => new KeyEvent(Key.F12),
                    _ => new KeyEvent(Key.None),
                };
            default:
                return new KeyEvent(Key.None);
        }
    }

    private static int ParseLeadingInt(ReadOnlySpan<byte> parameters)
    {
        int value = 0;
        bool any = false;
        foreach (byte c in parameters)
        {
            if (c is >= (byte)'0' and <= (byte)'9')
            {
                value = (value * 10) + (c - '0');
                any = true;
            }
            else
            {
                break;
            }
        }

        return any ? value : -1;
    }
}
