using System.Buffers.Binary;

namespace Schipper.Io.Ansi.Art;

/// <summary>
/// A parsed SAUCE record — the 128-byte metadata block appended to classic ANSI/ASCII art files,
/// optionally preceded by a comment block. For character art, <see cref="Width"/> and
/// <see cref="Height"/> come from the TInfo1/TInfo2 fields.
/// </summary>
public sealed class Sauce
{
    private const int RecordLength = 128;
    private const byte EofMarker = 0x1A;

    public required string Title { get; init; }
    public required string Author { get; init; }
    public required string Group { get; init; }
    public required string Date { get; init; }
    public uint FileSize { get; init; }
    public byte DataType { get; init; }
    public byte FileType { get; init; }
    public ushort TInfo1 { get; init; }
    public ushort TInfo2 { get; init; }
    public ushort TInfo3 { get; init; }
    public ushort TInfo4 { get; init; }
    public byte TFlags { get; init; }
    public required string FontName { get; init; }
    public required IReadOnlyList<string> Comments { get; init; }

    /// <summary>Declared art width in columns (TInfo1), valid for character-based types.</summary>
    public int Width => TInfo1;

    /// <summary>Declared art height in rows (TInfo2), valid for character-based types.</summary>
    public int Height => TInfo2;

    /// <summary>
    /// Attempts to parse a SAUCE record from the tail of <paramref name="file"/>. On success,
    /// <paramref name="dataLength"/> is the length of the actual art payload with the SAUCE record,
    /// comment block, and trailing EOF marker stripped.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> file, out Sauce? sauce, out int dataLength)
    {
        sauce = null;
        dataLength = file.Length;

        if (file.Length < RecordLength)
        {
            return false;
        }

        ReadOnlySpan<byte> rec = file[^RecordLength..];
        if (!(rec[0] == (byte)'S' && rec[1] == (byte)'A' && rec[2] == (byte)'U' && rec[3] == (byte)'C' && rec[4] == (byte)'E'))
        {
            return false;
        }

        byte commentCount = rec[104];

        var comments = new List<string>(commentCount);
        int sauceStart = file.Length - RecordLength;
        int commentBlockLength = 0;

        if (commentCount > 0)
        {
            commentBlockLength = 5 + (commentCount * 64);
            int commentStart = sauceStart - commentBlockLength;
            if (commentStart >= 0)
            {
                ReadOnlySpan<byte> block = file.Slice(commentStart, commentBlockLength);
                if (block[0] == (byte)'C' && block[1] == (byte)'O' && block[2] == (byte)'M' && block[3] == (byte)'N' && block[4] == (byte)'T')
                {
                    for (int i = 0; i < commentCount; i++)
                    {
                        comments.Add(ReadString(block, 5 + (i * 64), 64));
                    }
                }
                else
                {
                    // No valid comment block; treat the count as spurious.
                    commentBlockLength = 0;
                }
            }
            else
            {
                commentBlockLength = 0;
            }
        }

        sauce = new Sauce
        {
            Title = ReadString(rec, 7, 35),
            Author = ReadString(rec, 42, 20),
            Group = ReadString(rec, 62, 20),
            Date = ReadString(rec, 82, 8),
            FileSize = BinaryPrimitives.ReadUInt32LittleEndian(rec.Slice(90, 4)),
            DataType = rec[94],
            FileType = rec[95],
            TInfo1 = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(96, 2)),
            TInfo2 = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(98, 2)),
            TInfo3 = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(100, 2)),
            TInfo4 = BinaryPrimitives.ReadUInt16LittleEndian(rec.Slice(102, 2)),
            TFlags = rec[105],
            FontName = ReadString(rec, 106, 22),
            Comments = comments,
        };

        int dataEnd = sauceStart - commentBlockLength;
        if (dataEnd > 0 && file[dataEnd - 1] == EofMarker)
        {
            dataEnd--;
        }

        dataLength = Math.Max(0, dataEnd);
        return true;
    }

    private static string ReadString(ReadOnlySpan<byte> span, int offset, int length)
    {
        ReadOnlySpan<byte> field = span.Slice(offset, length);
        int nul = field.IndexOf((byte)0);
        if (nul >= 0)
        {
            field = field[..nul];
        }

        return Cp437.Decode(field).TrimEnd(' ');
    }
}
