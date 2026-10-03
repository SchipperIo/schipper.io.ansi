using System.Buffers.Binary;
using Schipper.Io.Ansi.Art;
using Schipper.Io.Ansi.Colors;
using Schipper.Io.Ansi.Screen;
using Schipper.Io.Ansi.Sequences;

namespace Schipper.Io.Ansi.Tests.Sequences;

[TestClass]
public sealed class AnsiParserTests
{
    [TestMethod]
    public void ParsesColoredGlyph()
    {
        // ESC[31m sets red foreground; 'A' is written at (0,0).
        byte[] art = "\x1b[31mA"u8.ToArray();
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);

        Cell cell = buffer[0, 0];
        Assert.AreEqual('A', cell.Glyph);
        Assert.AreEqual(ColorKind.Basic, cell.Foreground.Kind);
        Assert.AreEqual(BasicColor.Red, cell.Foreground.AsBasic);
    }

    [TestMethod]
    public void CursorPositioningPlacesGlyph()
    {
        // Move to row 2, col 3 then write 'Z'.
        byte[] art = "\x1b[2;3HZ"u8.ToArray();
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);
        Assert.AreEqual('Z', buffer[2, 1].Glyph);
    }

    [TestMethod]
    public void DecodesCp437BlockGlyph()
    {
        // 0xDB is the full block in CP437.
        ScreenBuffer buffer = AnsiParser.Parse([0xDB], width: 80);
        Assert.AreEqual('█', buffer[0, 0].Glyph);
    }

    [TestMethod]
    public void NewlineAdvancesRow()
    {
        byte[] art = "A\r\nB"u8.ToArray();
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);
        Assert.AreEqual('A', buffer[0, 0].Glyph);
        Assert.AreEqual('B', buffer[0, 1].Glyph);
    }

    [TestMethod]
    public void StripsSauceAndUsesDeclaredWidth()
    {
        byte[] art = BuildArtWithSauce("XY", width: 40, height: 1);
        ScreenBuffer buffer = AnsiParser.Parse(art);
        Assert.AreEqual(40, buffer.Width);
        Assert.AreEqual('X', buffer[0, 0].Glyph);
        Assert.AreEqual('Y', buffer[1, 0].Glyph);
    }

    [TestMethod]
    public void FullWidthLineDoesNotAddATrailingRow()
    {
        byte[] art = new byte[80];
        Array.Fill(art, (byte)'A');
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);

        Assert.AreEqual(1, buffer.Height);
        for (int x = 0; x < 80; x++)
        {
            Assert.AreEqual('A', buffer[x, 0].Glyph);
        }
    }

    [TestMethod]
    public void PendingWrapThenCrLfWritesOnTheNextRow()
    {
        byte[] art = new byte[82];
        Array.Fill(art, (byte)'A', 0, 80);
        art[80] = (byte)'\r';
        art[81] = (byte)'\n';
        byte[] withB = [.. art, (byte)'B'];
        ScreenBuffer buffer = AnsiParser.Parse(withB, width: 80);
        Assert.AreEqual('B', buffer[0, 1].Glyph);
    }

    [TestMethod]
    public void CrLfAfter79GlyphsStillWritesOnRow1()
    {
        byte[] art = new byte[81];
        Array.Fill(art, (byte)'A', 0, 79);
        art[79] = (byte)'\r';
        art[80] = (byte)'\n';
        byte[] withB = [.. art, (byte)'B'];
        ScreenBuffer buffer = AnsiParser.Parse(withB, width: 80);
        Assert.AreEqual('B', buffer[0, 1].Glyph);
    }

    [TestMethod]
    public void EraseDisplayUsesTheCurrentPenOnLaterRows()
    {
        byte[] art = "\x1b[44m\x1b[2J\x1b[3;1HX"u8.ToArray();
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);

        Assert.AreEqual(' ', buffer[0, 0].Glyph);
        Assert.AreEqual(BasicColor.Blue, buffer[0, 0].Background.AsBasic);
        Assert.AreEqual(' ', buffer[0, 1].Glyph);
        Assert.AreEqual(BasicColor.Blue, buffer[0, 1].Background.AsBasic);
        Assert.AreEqual('X', buffer[0, 2].Glyph);
        Assert.AreEqual(BasicColor.Blue, buffer[0, 2].Background.AsBasic);
    }

    [TestMethod]
    public void EraseLineUsesTheCurrentPen()
    {
        byte[] art = "\x1b[41m\x1b[K"u8.ToArray();
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);
        Assert.AreEqual(' ', buffer[79, 0].Glyph);
        Assert.AreEqual(BasicColor.Red, buffer[79, 0].Background.AsBasic);
    }

    [TestMethod]
    public void CupPastTheLastColumnClamps()
    {
        byte[] art = "\x1b[1;81HZ"u8.ToArray();
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);
        Assert.AreEqual('Z', buffer[79, 0].Glyph);
        Assert.AreEqual(1, buffer.Height);
    }

    [TestMethod]
    public void HugeCursorDownDoesNotThrowOrGrowPastMaxRows()
    {
        byte[] art = "\x1b[9999999999BA"u8.ToArray();
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);
        Assert.AreEqual(5000, buffer.Height);
        Assert.AreEqual('A', buffer[0, 4999].Glyph);
    }

    [TestMethod]
    public void HugeCupAllocatesAtMostMaxRows()
    {
        byte[] art = "\x1b[2000000;1HK"u8.ToArray();
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);
        Assert.IsLessThanOrEqualTo(5000, buffer.Height);
    }

    [TestMethod]
    public void SauceWidthAboveTheColumnCapIsIgnored()
    {
        byte[] art = BuildArtWithSauce("XY", width: 65535, height: 1);
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);
        Assert.AreEqual(80, buffer.Width);
        Assert.AreEqual('X', buffer[0, 0].Glyph);
    }

    [TestMethod]
    public void ParseRejectsWidthOutsideTheColumnCap()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            static () => AnsiParser.Parse([(byte)'A'], 100000));
    }

    [TestMethod]
    public void IceColorSelectsABrightBackgroundWithoutBlink()
    {
        ScreenBuffer ice = AnsiParser.Parse(BuildArtWithSauce("\x1b[5;44mX", width: 80, height: 1, tFlags: 1));
        Assert.AreEqual(BasicColor.BrightBlue, ice[0, 0].Background.AsBasic);
        Assert.AreEqual(CellAttributes.None, ice[0, 0].Attributes & CellAttributes.Blink);

        ScreenBuffer blink = AnsiParser.Parse(BuildArtWithSauce("\x1b[5;44mX", width: 80, height: 1, tFlags: 0));
        Assert.AreEqual(BasicColor.Blue, blink[0, 0].Background.AsBasic);
        Assert.AreEqual(CellAttributes.Blink, blink[0, 0].Attributes & CellAttributes.Blink);

        ScreenBuffer iceOrder = AnsiParser.Parse(BuildArtWithSauce("\x1b[44;5mX", width: 80, height: 1, tFlags: 1));
        Assert.AreEqual(BasicColor.BrightBlue, iceOrder[0, 0].Background.AsBasic);
        Assert.AreEqual(CellAttributes.None, iceOrder[0, 0].Attributes & CellAttributes.Blink);
    }

    [TestMethod]
    public void TabAdvancesToTheNextStop()
    {
        ScreenBuffer buffer = AnsiParser.Parse("A\tB"u8.ToArray(), width: 80);
        Assert.AreEqual('A', buffer[0, 0].Glyph);
        Assert.AreEqual('B', buffer[8, 0].Glyph);
        for (int x = 1; x < 8; x++)
        {
            Assert.AreEqual(' ', buffer[x, 0].Glyph);
        }
    }

    [TestMethod]
    public void TabOnTheLastColumnDoesNotAddARow()
    {
        byte[] art = new byte[80];
        Array.Fill(art, (byte)'A', 0, 79);
        art[79] = 0x09;
        ScreenBuffer buffer = AnsiParser.Parse(art, width: 80);
        Assert.AreEqual(1, buffer.Height);
    }

    [TestMethod]
    public void TruncatedExtendedColorDoesNotReplayAsBlinkOrFaint()
    {
        ScreenBuffer missingIndex = AnsiParser.Parse("\x1b[38;5mA"u8.ToArray(), width: 80);
        Assert.AreEqual(ColorKind.Default, missingIndex[0, 0].Foreground.Kind);
        Assert.AreEqual(CellAttributes.None, missingIndex[0, 0].Attributes & CellAttributes.Blink);

        ScreenBuffer missingRgb = AnsiParser.Parse("\x1b[38;2mA"u8.ToArray(), width: 80);
        Assert.AreEqual(CellAttributes.None, missingRgb[0, 0].Attributes & CellAttributes.Faint);

        ScreenBuffer palette = AnsiParser.Parse("\x1b[38;5;196mA"u8.ToArray(), width: 80);
        Assert.AreEqual(ColorKind.Palette256, palette[0, 0].Foreground.Kind);
        Assert.AreEqual(196, palette[0, 0].Foreground.PaletteIndex);

        ScreenBuffer laterRed = AnsiParser.Parse("\x1b[38;5;1;31mA"u8.ToArray(), width: 80);
        Assert.AreEqual(ColorKind.Basic, laterRed[0, 0].Foreground.Kind);
        Assert.AreEqual(BasicColor.Red, laterRed[0, 0].Foreground.AsBasic);
    }

    internal static byte[] BuildArtWithSauce(string text, ushort width, ushort height, byte tFlags = 0)
    {
        byte[] body = Cp437.Encode(text);
        byte[] record = new byte[128];
        WriteAscii(record, 0, "SAUCE");
        WriteAscii(record, 5, "00");
        WriteField(record, 7, "Test Title", 35);
        WriteField(record, 42, "Tester", 20);
        WriteField(record, 62, "Group", 20);
        WriteField(record, 82, "20260621", 8);
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(90, 4), (uint)body.Length);
        record[94] = 1; // DataType = Character
        record[95] = 1; // FileType = Ansi
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(96, 2), width);
        BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(98, 2), height);
        record[104] = 0; // no comments
        record[105] = tFlags;

        var file = new byte[body.Length + 1 + record.Length];
        body.CopyTo(file, 0);
        file[body.Length] = 0x1A; // EOF marker
        record.CopyTo(file, body.Length + 1);
        return file;
    }

    private static void WriteAscii(byte[] buffer, int offset, string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            buffer[offset + i] = (byte)value[i];
        }
    }

    private static void WriteField(byte[] buffer, int offset, string value, int length)
    {
        for (int i = 0; i < length; i++)
        {
            buffer[offset + i] = i < value.Length ? (byte)value[i] : (byte)' ';
        }
    }
}
