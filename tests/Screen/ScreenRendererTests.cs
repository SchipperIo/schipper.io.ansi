using System.Text;
using Schipper.Io.Ansi.Colors;
using Schipper.Io.Ansi.Screen;
using Schipper.Io.Ansi.Sequences;

namespace Schipper.Io.Ansi.Tests.Screen;

[TestClass]
public sealed class ScreenRendererTests
{
    [TestMethod]
    public void IdenticalBuffersProduceNoOutput()
    {
        var a = new ScreenBuffer(10, 3);
        var b = new ScreenBuffer(10, 3);
        Assert.AreEqual(string.Empty, ScreenRenderer.RenderDiff(a, b));
    }

    [TestMethod]
    public void SingleChangedCellMovesCursorAndWritesGlyph()
    {
        var previous = new ScreenBuffer(10, 3);
        var current = previous.Clone();
        current.Set(2, 1, 'X', Color.Basic(BasicColor.BrightCyan), Color.Default);

        string output = ScreenRenderer.RenderDiff(previous, current);

        // Cursor move to row 2, column 3 (1-based) then the glyph.
        StringAssert.Contains(output, "\x1b[2;3H");
        StringAssert.Contains(output, "X");
    }

    [TestMethod]
    public void DimensionChangeForcesFullRender()
    {
        var previous = new ScreenBuffer(5, 2);
        var current = new ScreenBuffer(8, 2);
        string output = ScreenRenderer.RenderDiff(previous, current);
        StringAssert.Contains(output, "\x1b[2J");
    }

    [TestMethod]
    public void DrawTextClipsAtRightEdge()
    {
        var buffer = new ScreenBuffer(4, 1);
        buffer.DrawText(2, 0, "ABCD", Color.Default, Color.Default);
        Assert.AreEqual('A', buffer[2, 0].Glyph);
        Assert.AreEqual('B', buffer[3, 0].Glyph);
    }

    // ── ColorDepth overloads ────────────────────────────────────────────────

    [TestMethod]
    public void TrueColorBufferAtBasic16EmitsOnlyBasicSgr()
    {
        var buffer = new ScreenBuffer(2, 1);
        buffer.Set(0, 0, 'A', Color.Rgb(255, 0, 0), Color.Default);
        buffer.Set(1, 0, 'B', Color.Rgb(250, 10, 5), Color.Default);

        string output = ScreenRenderer.RenderComplete(buffer, ColorDepth.Basic16);

        Assert.IsFalse(output.Contains("38;2", StringComparison.Ordinal), "truecolor SGR leaked through Basic16 downgrade");
        Assert.IsFalse(output.Contains("38;5", StringComparison.Ordinal), "palette SGR leaked through Basic16 downgrade");
        StringAssert.Contains(output, "\x1b[0;91;49m"); // both cells downgrade to BrightRed fg.
    }

    [TestMethod]
    public void CellsCollapsingToSameBasicColorShareOneSgrRun()
    {
        var buffer = new ScreenBuffer(2, 1);
        buffer.Set(0, 0, 'A', Color.Rgb(255, 0, 0), Color.Default);
        buffer.Set(1, 0, 'B', Color.Rgb(250, 10, 5), Color.Default);

        // At truecolor depth the two distinct RGB values need two SGR runs; at Basic16 they collapse
        // to the same BrightRed and must share a single run.
        Assert.AreEqual(2, CountOccurrences(ScreenRenderer.RenderComplete(buffer, ColorDepth.TrueColor), "\x1b[0;"));
        Assert.AreEqual(1, CountOccurrences(ScreenRenderer.RenderComplete(buffer, ColorDepth.Basic16), "\x1b[0;"));
    }

    [TestMethod]
    public void TrueColorBufferAtPalette256EmitsPaletteSgr()
    {
        var buffer = new ScreenBuffer(1, 1);
        buffer.Set(0, 0, 'X', Color.Rgb(95, 135, 175), Color.Default);

        string output = ScreenRenderer.RenderComplete(buffer, ColorDepth.Palette256);

        Assert.IsFalse(output.Contains("38;2", StringComparison.Ordinal));
        StringAssert.Contains(output, "38;5;67"); // exact cube hit.
    }

    [TestMethod]
    public void DepthOverloadOnDiffDowngradesChangedCells()
    {
        var previous = new ScreenBuffer(4, 2);
        var current = previous.Clone();
        current.Set(1, 1, 'X', Color.Rgb(255, 0, 0), Color.Default);

        string output = ScreenRenderer.RenderDiff(previous, current, ColorDepth.Basic16);

        StringAssert.Contains(output, "\x1b[2;2H");
        StringAssert.Contains(output, "\x1b[0;91;49m");
        Assert.IsFalse(output.Contains("38;2", StringComparison.Ordinal));
    }

    // ── StringBuilder overloads ─────────────────────────────────────────────

    [TestMethod]
    public void StringBuilderOverloadsMatchStringVersions()
    {
        var previous = new ScreenBuffer(6, 3);
        var current = previous.Clone();
        current.Set(2, 1, 'X', Color.Rgb(1, 2, 3), Color.Basic(BasicColor.Blue));
        current.Set(5, 2, 'Y', Color.Palette(200), Color.Default);

        var sb = new StringBuilder();

        ScreenRenderer.RenderFull(current, sb);
        Assert.AreEqual(ScreenRenderer.RenderFull(current), sb.ToString());

        ScreenRenderer.RenderComplete(current, sb);
        Assert.AreEqual(ScreenRenderer.RenderComplete(current), sb.ToString());

        ScreenRenderer.RenderDiff(previous, current, sb);
        Assert.AreEqual(ScreenRenderer.RenderDiff(previous, current), sb.ToString());

        ScreenRenderer.RenderDiff(previous, current, sb, ColorDepth.Basic16);
        Assert.AreEqual(ScreenRenderer.RenderDiff(previous, current, ColorDepth.Basic16), sb.ToString());
    }

    [TestMethod]
    public void StringBuilderOverloadClearsBuilderBeforeRendering()
    {
        var buffer = new ScreenBuffer(3, 1);
        buffer.Set(0, 0, 'Z', Color.Default, Color.Default);

        var sb = new StringBuilder("stale content from a previous frame");
        ScreenRenderer.RenderComplete(buffer, sb);

        Assert.AreEqual(ScreenRenderer.RenderComplete(buffer), sb.ToString());
    }

    [TestMethod]
    public void StringBuilderDiffLeavesBuilderEmptyWhenNothingChanged()
    {
        var a = new ScreenBuffer(10, 3);
        var b = new ScreenBuffer(10, 3);
        var sb = new StringBuilder("stale");
        ScreenRenderer.RenderDiff(a, b, sb);
        Assert.AreEqual(0, sb.Length);
    }

    // ── Synchronized output ─────────────────────────────────────────────────

    [TestMethod]
    public void BeginSyncAndEndSyncEmitExactSequences()
    {
        var sb = new StringBuilder();
        global::Schipper.Io.Ansi.Sequences.Ansi.BeginSync(sb);
        Assert.AreEqual("\x1b[?2026h", sb.ToString());

        sb.Clear();
        global::Schipper.Io.Ansi.Sequences.Ansi.EndSync(sb);
        Assert.AreEqual("\x1b[?2026l", sb.ToString());
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
