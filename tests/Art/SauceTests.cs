using Schipper.Io.Ansi.Art;
using Schipper.Io.Ansi.Tests.Sequences;

namespace Schipper.Io.Ansi.Tests.Art;

[TestClass]
public sealed class SauceTests
{
    [TestMethod]
    public void ParsesMetadataFields()
    {
        byte[] file = AnsiParserTests.BuildArtWithSauce("ABC", width: 80, height: 25);

        Assert.IsTrue(Sauce.TryParse(file, out Sauce? sauce, out int dataLength));
        Assert.IsNotNull(sauce);
        Assert.AreEqual("Test Title", sauce!.Title);
        Assert.AreEqual("Tester", sauce.Author);
        Assert.AreEqual(80, sauce.Width);
        Assert.AreEqual(25, sauce.Height);

        // Data length should exclude the EOF marker and the SAUCE record (art body is "ABC" = 3 bytes).
        Assert.AreEqual(3, dataLength);
    }

    [TestMethod]
    public void ReturnsFalseWhenNoSauceRecord()
    {
        byte[] file = "just art, no metadata here at all"u8.ToArray();
        Assert.IsFalse(Sauce.TryParse(file, out Sauce? sauce, out _));
        Assert.IsNull(sauce);
    }

    [TestMethod]
    public void StopsTextFieldsAtTheFirstNul()
    {
        byte[] file = BuildRecord(title: [.. "Test"u8, 0x00, .. "XXXX"u8], font: "IBM VGA");
        Assert.IsTrue(Sauce.TryParse(file, out Sauce? sauce, out _));
        Assert.AreEqual("Test", sauce!.Title);
        Assert.AreEqual("IBM VGA", sauce.FontName);
    }

    [TestMethod]
    public void SpacePaddedTitleStillParses()
    {
        byte[] file = AnsiParserTests.BuildArtWithSauce("ABC", width: 80, height: 25);
        Assert.IsTrue(Sauce.TryParse(file, out Sauce? sauce, out _));
        Assert.AreEqual("Test Title", sauce!.Title);
    }

    private static byte[] BuildRecord(ReadOnlySpan<byte> title, string font)
    {
        byte[] record = new byte[128];
        WriteAscii(record, 0, "SAUCE00");
        for (int i = 0; i < 35; i++)
        {
            record[7 + i] = i < title.Length ? title[i] : (byte)' ';
        }

        WriteField(record, 106, font, 22);
        return record;
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
            buffer[offset + i] = i < value.Length ? (byte)value[i] : (byte)0;
        }
    }
}
