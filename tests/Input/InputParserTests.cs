using Schipper.Io.Ansi.Input;

namespace Schipper.Io.Ansi.Tests.Input;

[TestClass]
public sealed class InputParserTests
{
    [TestMethod]
    public void DecodesArrowKeys()
    {
        var parser = new InputParser();
        var events = parser.Feed("\x1b[A"u8);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Up, events[0].Key);
    }

    [TestMethod]
    public void HandlesSequenceSplitAcrossReads()
    {
        var parser = new InputParser();
        Assert.AreEqual(0, parser.Feed("\x1b"u8).Count);
        Assert.AreEqual(0, parser.Feed("["u8).Count);
        var events = parser.Feed("C"u8);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Right, events[0].Key);
    }

    [TestMethod]
    public void CollapsesCrLfIntoSingleEnter()
    {
        var parser = new InputParser();
        var events = parser.Feed("\r\n"u8);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Enter, events[0].Key);
    }

    [TestMethod]
    public void DecodesControlAsCtrlChar()
    {
        var parser = new InputParser();
        var events = parser.Feed([0x01]);
        Assert.AreEqual(1, events.Count);
        Assert.IsTrue(events[0].Ctrl);
        Assert.AreEqual('a', events[0].Char);
    }

    [TestMethod]
    public void DecodesFunctionKeyViaTilde()
    {
        var parser = new InputParser();
        var events = parser.Feed("\x1b[15~"u8);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.F5, events[0].Key);
    }

    [TestMethod]
    public void DecodesPrintableCharacters()
    {
        var parser = new InputParser();
        var events = parser.Feed("Hi"u8);
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual('H', events[0].Char);
        Assert.AreEqual('i', events[1].Char);
    }

    [TestMethod]
    public void FlushResolvesLoneEscape()
    {
        var parser = new InputParser();
        Assert.AreEqual(0, parser.Feed("\x1b"u8).Count);
        KeyEvent? flushed = parser.Flush();
        Assert.IsNotNull(flushed);
        Assert.AreEqual(Key.Escape, flushed!.Value.Key);
    }

    // ── Pending-buffer rewrite regressions ──────────────────────────────────

    [TestMethod]
    public void PartialCsiWithParametersAcrossFeeds()
    {
        var parser = new InputParser();
        Assert.AreEqual(0, parser.Feed("\x1b[1"u8).Count);
        Assert.AreEqual(3, parser.PendingCount);
        var events = parser.Feed("5~"u8);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.F5, events[0].Key);
        Assert.AreEqual(0, parser.PendingCount);
    }

    [TestMethod]
    public void LargePasteDecodesEveryByte()
    {
        var parser = new InputParser();
        byte[] paste = new byte[10_000];
        Array.Fill(paste, (byte)'a');

        var events = parser.Feed(paste);

        Assert.AreEqual(10_000, events.Count);
        Assert.AreEqual('a', events[0].Char);
        Assert.AreEqual('a', events[^1].Char);
        Assert.AreEqual(0, parser.PendingCount);
    }

    [TestMethod]
    public void InterleavedTextAndPartialSequencesAcrossFeeds()
    {
        var parser = new InputParser();

        var first = parser.Feed("A\x1b["u8);
        Assert.AreEqual(1, first.Count);
        Assert.AreEqual('A', first[0].Char);
        Assert.AreEqual(2, parser.PendingCount);

        var second = parser.Feed("B\x1b"u8);
        Assert.AreEqual(1, second.Count);
        Assert.AreEqual(Key.Down, second[0].Key); // CSI B
        Assert.IsTrue(parser.PendingIsLoneEscape);

        var third = parser.Feed("OC"u8);
        Assert.AreEqual(1, third.Count);
        Assert.AreEqual(Key.Right, third[0].Key); // SS3 C
        Assert.AreEqual(0, parser.PendingCount);
    }

    [TestMethod]
    public void ManyPartialFeedsKeepDecodingCorrectly()
    {
        var parser = new InputParser();
        for (int i = 0; i < 1_000; i++)
        {
            Assert.AreEqual(0, parser.Feed("\x1b["u8).Count);
            var events = parser.Feed("A"u8);
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(Key.Up, events[0].Key);
        }

        Assert.AreEqual(0, parser.PendingCount);
    }

    [TestMethod]
    public void CallerListOverloadAppendsWithoutClearing()
    {
        var parser = new InputParser();
        var events = new List<KeyEvent> { new(Key.Enter) };

        parser.Feed("x"u8, events);

        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(Key.Enter, events[0].Key);
        Assert.AreEqual('x', events[1].Char);
    }

    [TestMethod]
    public void ReturnedListIsReusedByNextFeed()
    {
        // Documented behavior: the list returned by Feed is an internal buffer valid only until the
        // next Feed call.
        var parser = new InputParser();
        var first = parser.Feed("A"u8);
        var second = parser.Feed("B"u8);
        Assert.AreSame(first, second);
        Assert.AreEqual('B', second[0].Char);
    }

    [TestMethod]
    public void FlushDiscardsPartialSequenceAndReportsEscape()
    {
        var parser = new InputParser();
        Assert.AreEqual(0, parser.Feed("\x1b["u8).Count);

        KeyEvent? flushed = parser.Flush();
        Assert.IsNotNull(flushed);
        Assert.AreEqual(Key.Escape, flushed!.Value.Key);
        Assert.AreEqual(0, parser.PendingCount);

        IReadOnlyList<KeyEvent> events = parser.Feed("\x1b[C"u8);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Right, events[0].Key);
    }

    [TestMethod]
    public void DecodesUtf8Character()
    {
        InputParser parser = new();
        IReadOnlyList<KeyEvent> events = parser.Feed([0xC3, 0xA9]);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Char, events[0].Key);
        Assert.AreEqual('é', events[0].Char);
    }

    [TestMethod]
    public void ReassemblesUtf8SplitAcrossFeeds()
    {
        InputParser parser = new();
        Assert.AreEqual(0, parser.Feed([0xC3]).Count);
        IReadOnlyList<KeyEvent> events = parser.Feed([0xA9]);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual('é', events[0].Char);
    }

    [TestMethod]
    public void DecodesUtf8BoxDrawingAsOneCharacter()
    {
        InputParser parser = new();
        IReadOnlyList<KeyEvent> events = parser.Feed([0xE2, 0x94, 0x80]);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual('\u2500', events[0].Char);
    }

    [TestMethod]
    public void EscapePlusUtf8IsAltCharacter()
    {
        InputParser parser = new();
        IReadOnlyList<KeyEvent> events = parser.Feed([0x1B, 0xC3, 0xA9]);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual('é', events[0].Char);
        Assert.IsTrue(events[0].Alt);
    }

    [TestMethod]
    public void UnfinishedCsiThenCrIsEnter()
    {
        InputParser parser = new();
        Assert.AreEqual(0, parser.Feed("\x1b["u8).Count);
        IReadOnlyList<KeyEvent> events = parser.Feed("\r"u8);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Enter, events[0].Key);
        Assert.AreEqual(0, parser.PendingCount);
    }

    [TestMethod]
    public void NewEscapeAbortsUnfinishedCsiSoSplitF5ThenArrowBothArrive()
    {
        InputParser parser = new();
        Assert.AreEqual(0, parser.Feed("\x1b[15"u8).Count);
        IReadOnlyList<KeyEvent> events = parser.Feed("\x1b[A"u8);
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(Key.F5, events[0].Key);
        Assert.AreEqual(Key.Up, events[1].Key);
    }

    [TestMethod]
    public void PendingCsiThenUtf8YieldsTheCharacter()
    {
        InputParser parser = new();
        Assert.AreEqual(0, parser.Feed("\x1b["u8).Count);
        IReadOnlyList<KeyEvent> events = parser.Feed([0xC3, 0xA9]);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual('é', events[0].Char);
        Assert.AreEqual(0, parser.PendingCount);
    }

    [TestMethod]
    public void OverlongCsiDoesNotLeavePendingGrowing()
    {
        InputParser parser = new();
        parser.Feed("\x1b["u8);
        byte[] junk = new byte[80];
        Array.Fill(junk, (byte)'0');
        parser.Feed(junk);
        Assert.IsLessThan(64, parser.PendingCount);
    }

    [TestMethod]
    public void SplitCrLfIsOneEnter()
    {
        InputParser parser = new();
        Assert.AreEqual(0, parser.Feed("\r"u8).Count);
        IReadOnlyList<KeyEvent> events = parser.Feed("\n"u8);
        Assert.AreEqual(1, events.Count);
        Assert.AreEqual(Key.Enter, events[0].Key);
        Assert.AreEqual(0, parser.PendingCount);
    }

    [TestMethod]
    public void FlushOfHeldCrIsEnter()
    {
        InputParser parser = new();
        Assert.AreEqual(0, parser.Feed("\r"u8).Count);
        KeyEvent? flushed = parser.Flush();
        Assert.IsNotNull(flushed);
        Assert.AreEqual(Key.Enter, flushed!.Value.Key);
    }

    [TestMethod]
    public void CrFollowedByACharacterIsEnterThenTheCharacter()
    {
        InputParser parser = new();
        Assert.AreEqual(0, parser.Feed("\r"u8).Count);
        IReadOnlyList<KeyEvent> events = parser.Feed("X"u8);
        Assert.AreEqual(2, events.Count);
        Assert.AreEqual(Key.Enter, events[0].Key);
        Assert.AreEqual('X', events[1].Char);
    }
}
