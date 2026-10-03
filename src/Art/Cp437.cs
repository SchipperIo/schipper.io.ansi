namespace Schipper.Io.Ansi.Art;

/// <summary>
/// Hand-coded IBM PC code page 437 codec. Maps each of the 256 bytes to its Unicode glyph and
/// back, so authentic BBS/ANSI art (box drawing, block elements, shaded blocks) renders correctly
/// without depending on the <c>System.Text.Encoding.CodePages</c> NuGet package.
/// </summary>
public static class Cp437
{
    // Byte -> Unicode glyph. The C0 region (0x00-0x1F) uses the classic CP437 dingbat glyphs;
    // callers that treat those bytes as control codes should do so before decoding.
    private static readonly char[] ToUnicodeTable = BuildToUnicode();

    // Reverse map, built once. Index is the Unicode code point (BMP only); value is the CP437 byte,
    // or 0 when unmapped. Entry 0 is handled explicitly so the default-zero is unambiguous.
    private static readonly byte[] FromUnicodeTable = BuildFromUnicode();

    /// <summary>The substitute byte emitted when a character has no CP437 representation.</summary>
    public const byte Substitute = (byte)'?';

    /// <summary>Returns the Unicode glyph for a CP437 byte.</summary>
    public static char ToChar(byte b) => ToUnicodeTable[b];

    /// <summary>Returns the CP437 byte for a Unicode character, or <see cref="Substitute"/> if unmapped.</summary>
    public static byte FromChar(char c)
    {
        // ASCII and C0 control codes (ESC, CR, LF, Tab, …) pass through unchanged so that encoding an
        // outgoing terminal stream preserves escape sequences. Extended glyphs use the reverse table.
        if (c < 0x80)
        {
            return (byte)c;
        }

        if (c < FromUnicodeTable.Length)
        {
            byte mapped = FromUnicodeTable[c];
            if (mapped != 0)
            {
                return mapped;
            }
        }

        return Substitute;
    }

    /// <summary>Decodes CP437 bytes to a Unicode string.</summary>
    public static string Decode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        return string.Create(bytes.Length, bytes.ToArray(), static (span, src) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = ToUnicodeTable[src[i]];
            }
        });
    }

    /// <summary>Encodes a Unicode string to CP437 bytes, substituting unmapped characters.</summary>
    public static byte[] Encode(ReadOnlySpan<char> text)
    {
        var result = new byte[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            result[i] = FromChar(text[i]);
        }

        return result;
    }

    private static byte[] BuildFromUnicode()
    {
        // Covers every box-drawing/block/Latin glyph used for encoding art. A handful of C0-region
        // dingbats (smileys, card suits) live above this range and are decode-only by design.
        var table = new byte[0x2600];
        for (int b = 0; b < 256; b++)
        {
            char c = ToUnicodeTable[b];
            if (c >= table.Length)
            {
                continue;
            }

            // Prefer the first byte that maps to a given glyph (ASCII before dingbat duplicates).
            if (table[c] == 0)
            {
                table[c] = (byte)b;
            }
        }

        return table;
    }

    private static char[] BuildToUnicode()
    {
        // 0x00-0x1F: classic CP437 control-region glyphs.
        const string c0 =
            " ☺☻♥♦♣♠•◘○◙♂♀♪♫☼" +
            "►◄↕‼¶§▬↨↑↓→←∟↔▲▼";

        // 0x20-0x7E: printable ASCII. 0x7F: house glyph.
        const string ascii =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~⌂";

        // 0x80-0xFF: extended Latin, currency, box drawing, block elements, Greek, and math.
        const string high =
            "ÇüéâäàåçêëèïîìÄÅ" +
            "ÉæÆôöòûùÿÖÜ¢£¥₧ƒ" +
            "áíóúñÑªº¿⌐¬½¼¡«»" +
            "░▒▓│┤╡╢╖╕╣║╗╝╜╛┐" +
            "└┴┬├─┼╞╟╚╔╩╦╠═╬╧" +
            "╨╤╥╙╘╒╓╫╪┘┌█▄▌▐▀" +
            "αßΓπΣσµτΦΘΩδ∞φε∩" +
            "≡±≥≤⌠⌡÷≈°∙·√ⁿ²■ ";

        string all = c0 + ascii + high;
        if (all.Length != 256)
        {
            throw new InvalidOperationException(
                $"CP437 glyph table must contain exactly 256 entries but had {all.Length}.");
        }

        return all.ToCharArray();
    }
}
