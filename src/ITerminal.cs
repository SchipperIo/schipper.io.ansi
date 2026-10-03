using System.Buffers;
using System.Text;

namespace Schipper.Io.Ansi;

/// <summary>
/// A glyph encoding hint. The SSH wire is always UTF-8; this enum survives only as a value in the
/// door protocol's handshake (a game may use it to pick glyphs). CP437 also remains the on-disk
/// ANSI-art format via <see cref="Cp437"/>.
/// </summary>
public enum TerminalEncoding : byte
{
    /// <summary>Classic IBM PC code page 437 (authentic BBS art).</summary>
    Cp437 = 0,

    /// <summary>UTF-8 (modern SSH clients).</summary>
    Utf8 = 1,
}

/// <summary>
/// A connected, full-duplex terminal. Implemented by the SSH session and consumed by the TUI. The
/// wire is always UTF-8 (modern SSH clients); CP437 lives on only as the on-disk ANSI-art format (see
/// <see cref="Cp437"/>), not as a transport encoding. All sizes are in character cells;
/// <see cref="Resized"/> fires when the remote reports a new window size.
/// </summary>
public interface ITerminal
{
    int Columns { get; }

    int Rows { get; }

    /// <summary>Raised when the remote window size changes (SSH window-change).</summary>
    event Action? Resized;

    ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default);

    /// <summary>Reads available bytes into <paramref name="buffer"/>; returns 0 at end of stream.</summary>
    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}

/// <summary>Convenience helpers for writing UTF-8 text/ANSI to an <see cref="ITerminal"/>.</summary>
public static class TerminalExtensions
{
    /// <summary>Encodes a string as UTF-8. ANSI escape bytes are ASCII and pass through unchanged.</summary>
    public static byte[] Encode(this ITerminal terminal, string text) => Encoding.UTF8.GetBytes(text);

    public static ValueTask WriteAsync(this ITerminal terminal, string text, CancellationToken cancellationToken = default) =>
        terminal.WriteAsync(terminal.Encode(text), cancellationToken);

    /// <summary>
    /// Writes a <see cref="StringBuilder"/> as UTF-8 without materializing a string or a per-call
    /// byte array: chunks are encoded into a single <see cref="ArrayPool{T}"/> rented buffer (a
    /// stateful <see cref="Encoder"/> keeps surrogate pairs that straddle chunk boundaries intact)
    /// and the buffer is returned to the pool after the write completes.
    /// </summary>
    public static ValueTask WriteAsync(this ITerminal terminal, StringBuilder text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terminal);
        ArgumentNullException.ThrowIfNull(text);
        return text.Length == 0 ? ValueTask.CompletedTask : WriteCoreAsync(terminal, text, cancellationToken);

        static async ValueTask WriteCoreAsync(ITerminal terminal, StringBuilder text, CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(text.Length));
            try
            {
                Encoder encoder = Encoding.UTF8.GetEncoder();
                int written = 0;
                foreach (ReadOnlyMemory<char> chunk in text.GetChunks())
                {
                    written += encoder.GetBytes(chunk.Span, buffer.AsSpan(written), flush: false);
                }

                written += encoder.GetBytes(ReadOnlySpan<char>.Empty, buffer.AsSpan(written), flush: true);
                await terminal.WriteAsync(buffer.AsMemory(0, written), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
