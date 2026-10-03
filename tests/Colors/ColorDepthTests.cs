using Schipper.Io.Ansi.Colors;

namespace Schipper.Io.Ansi.Tests.Colors;

[TestClass]
public sealed class ColorDepthTests
{
    // ── TrueColor → Palette256 ──────────────────────────────────────────────

    [TestMethod]
    public void TrueColorExactCubeHitMapsToExactIndex()
    {
        // (95,135,175) = cube levels (1,2,3) → 16 + 36*1 + 6*2 + 3 = 67.
        Assert.AreEqual(Color.Palette(67), Color.Rgb(95, 135, 175).Downgrade(ColorDepth.Palette256));
    }

    [TestMethod]
    public void TrueColorCubeCornersMapExactly()
    {
        Assert.AreEqual(Color.Palette(16), Color.Rgb(0, 0, 0).Downgrade(ColorDepth.Palette256));
        Assert.AreEqual(Color.Palette(231), Color.Rgb(255, 255, 255).Downgrade(ColorDepth.Palette256));
        Assert.AreEqual(Color.Palette(196), Color.Rgb(255, 0, 0).Downgrade(ColorDepth.Palette256));
        Assert.AreEqual(Color.Palette(21), Color.Rgb(0, 0, 255).Downgrade(ColorDepth.Palette256));
    }

    [TestMethod]
    public void TrueColorGrayRampHitsMapExactly()
    {
        // Gray ramp entry n has value 8 + 10n at index 232 + n.
        Assert.AreEqual(Color.Palette(232), Color.Rgb(8, 8, 8).Downgrade(ColorDepth.Palette256));
        Assert.AreEqual(Color.Palette(244), Color.Rgb(128, 128, 128).Downgrade(ColorDepth.Palette256));
        Assert.AreEqual(Color.Palette(255), Color.Rgb(238, 238, 238).Downgrade(ColorDepth.Palette256));
    }

    [TestMethod]
    public void TrueColorNearMissPicksNearestEntry()
    {
        // (0,0,1) is 1 away from cube black (index 16) but far from gray 8 (index 232).
        Assert.AreEqual(Color.Palette(16), Color.Rgb(0, 0, 1).Downgrade(ColorDepth.Palette256));

        // (10,10,10) is closest to gray 8 (index 232), not cube black.
        Assert.AreEqual(Color.Palette(232), Color.Rgb(10, 10, 10).Downgrade(ColorDepth.Palette256));

        // (250,5,5) is closest to cube (255,0,0) = index 196.
        Assert.AreEqual(Color.Palette(196), Color.Rgb(250, 5, 5).Downgrade(ColorDepth.Palette256));
    }

    // ── → Basic16 ───────────────────────────────────────────────────────────

    [TestMethod]
    public void Palette256IndexMapsToNearestBasic()
    {
        // 196 = rgb(255,0,0) → BrightRed (exact). 21 = rgb(0,0,255) → Blue (0,0,238).
        Assert.AreEqual(Color.Basic(BasicColor.BrightRed), Color.Palette(196).Downgrade(ColorDepth.Basic16));
        Assert.AreEqual(Color.Basic(BasicColor.Blue), Color.Palette(21).Downgrade(ColorDepth.Basic16));
        Assert.AreEqual(Color.Basic(BasicColor.Black), Color.Palette(16).Downgrade(ColorDepth.Basic16));
        Assert.AreEqual(Color.Basic(BasicColor.BrightWhite), Color.Palette(231).Downgrade(ColorDepth.Basic16));
    }

    [TestMethod]
    public void PaletteBasicRangePassesThroughAsBasic()
    {
        // Indices 0-15 are the basic colors themselves.
        Assert.AreEqual(Color.Basic(BasicColor.BrightRed), Color.Palette(9).Downgrade(ColorDepth.Basic16));
        Assert.AreEqual(Color.Basic(BasicColor.Black), Color.Palette(0).Downgrade(ColorDepth.Basic16));
    }

    [TestMethod]
    public void GrayRampIndexMapsToNearestBasicGray()
    {
        // 232 = rgb(8,8,8) → Black; 255 = rgb(238,238,238) → White (229,229,229).
        Assert.AreEqual(Color.Basic(BasicColor.Black), Color.Palette(232).Downgrade(ColorDepth.Basic16));
        Assert.AreEqual(Color.Basic(BasicColor.White), Color.Palette(255).Downgrade(ColorDepth.Basic16));
        // 244 = rgb(128,128,128) → BrightBlack (127,127,127).
        Assert.AreEqual(Color.Basic(BasicColor.BrightBlack), Color.Palette(244).Downgrade(ColorDepth.Basic16));
    }

    [TestMethod]
    public void TrueColorMapsDirectlyToNearestBasic()
    {
        Assert.AreEqual(Color.Basic(BasicColor.BrightRed), Color.Rgb(255, 0, 0).Downgrade(ColorDepth.Basic16));
        Assert.AreEqual(Color.Basic(BasicColor.Red), Color.Rgb(200, 10, 10).Downgrade(ColorDepth.Basic16));
        Assert.AreEqual(Color.Basic(BasicColor.BrightWhite), Color.Rgb(255, 255, 255).Downgrade(ColorDepth.Basic16));
        Assert.AreEqual(Color.Basic(BasicColor.Black), Color.Rgb(5, 5, 5).Downgrade(ColorDepth.Basic16));
    }

    // ── Passthrough / no-op ─────────────────────────────────────────────────

    [TestMethod]
    public void DefaultColorPassesThroughAtEveryDepth()
    {
        Assert.AreEqual(Color.Default, Color.Default.Downgrade(ColorDepth.TrueColor));
        Assert.AreEqual(Color.Default, Color.Default.Downgrade(ColorDepth.Palette256));
        Assert.AreEqual(Color.Default, Color.Default.Downgrade(ColorDepth.Basic16));
    }

    [TestMethod]
    public void BasicColorPassesThroughAtEveryDepth()
    {
        Color cyan = Color.Basic(BasicColor.BrightCyan);
        Assert.AreEqual(cyan, cyan.Downgrade(ColorDepth.TrueColor));
        Assert.AreEqual(cyan, cyan.Downgrade(ColorDepth.Palette256));
        Assert.AreEqual(cyan, cyan.Downgrade(ColorDepth.Basic16));
    }

    [TestMethod]
    public void DowngradeToSatisfiedDepthIsNoOp()
    {
        Color palette = Color.Palette(200);
        Assert.AreEqual(palette, palette.Downgrade(ColorDepth.TrueColor));
        Assert.AreEqual(palette, palette.Downgrade(ColorDepth.Palette256));

        Color rgb = Color.Rgb(1, 2, 3);
        Assert.AreEqual(rgb, rgb.Downgrade(ColorDepth.TrueColor));
    }

    [TestMethod]
    public void EveryPaletteIndexDowngradesToAValidBasicColor()
    {
        for (int i = 0; i < 256; i++)
        {
            Color downgraded = Color.Palette((byte)i).Downgrade(ColorDepth.Basic16);
            Assert.AreEqual(ColorKind.Basic, downgraded.Kind);
            Assert.IsTrue((byte)downgraded.AsBasic < 16);
        }
    }
}
