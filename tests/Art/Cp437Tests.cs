using Schipper.Io.Ansi.Art;

namespace Schipper.Io.Ansi.Tests.Art;

[TestClass]
public sealed class Cp437Tests
{
    [TestMethod]
    public void DecodesFullBlock() => Assert.AreEqual('█', Cp437.ToChar(0xDB));

    [TestMethod]
    public void EncodesBoxDrawing() => Assert.AreEqual((byte)0xC9, Cp437.FromChar('╔'));

    [TestMethod]
    public void RoundTripsEncodableByteValues()
    {
        // 0x20-0xFF (ASCII, extended Latin, box drawing, blocks) all round-trip. The C0 region
        // (0x00-0x1F) holds a few decode-only dingbats that live above the reverse-map range.
        for (int b = 0x20; b < 256; b++)
        {
            char c = Cp437.ToChar((byte)b);
            byte back = Cp437.FromChar(c);
            Assert.AreEqual(c, Cp437.ToChar(back), $"glyph round-trip failed at byte 0x{b:X2}");
        }
    }

    [TestMethod]
    public void DecodesAsciiUnchanged()
    {
        byte[] bytes = "Shelly"u8.ToArray();
        Assert.AreEqual("Shelly", Cp437.Decode(bytes));
    }

    [TestMethod]
    public void SubstitutesUnmappedCharacters() => Assert.AreEqual(Cp437.Substitute, Cp437.FromChar('退'));
}
