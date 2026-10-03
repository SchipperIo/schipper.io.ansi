using System.Text;
using Schipper.Io.Ansi;
using Schipper.Io.Ansi.Sequences;

namespace Schipper.Io.Ansi.Tests;

[TestClass]
public sealed class TerminalExtensionsTests
{
    [TestMethod]
    public async Task StringBuilderWriteRoundTripsUtf8()
    {
        var terminal = new CaptureTerminal();
        var sb = new StringBuilder("Hello, world \x1b[1;1H");

        await terminal.WriteAsync(sb);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(sb.ToString()), terminal.Captured.ToArray());
    }

    [TestMethod]
    public async Task EmptyStringBuilderWritesNothing()
    {
        var terminal = new CaptureTerminal();
        await terminal.WriteAsync(new StringBuilder());
        Assert.AreEqual(0, terminal.Captured.Count);
        Assert.AreEqual(0, terminal.WriteCalls);
    }

    [TestMethod]
    public async Task MultiChunkBuilderWithMultiByteUtf8RoundTrips()
    {
        var terminal = new CaptureTerminal();

        // Well past StringBuilder's max chunk size (8000 chars) so GetChunks() yields several
        // chunks, with em dashes, CJK, and astral-plane emoji sprinkled throughout.
        var sb = new StringBuilder();
        for (int i = 0; i < 2_000; i++)
        {
            sb.Append("line — 漢字テスト 🚀🛰️ ").Append(i);
        }

        Assert.IsTrue(CountChunks(sb) > 1, "test needs a multi-chunk StringBuilder");

        await terminal.WriteAsync(sb);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(sb.ToString()), terminal.Captured.ToArray());
    }

    [TestMethod]
    public async Task SurrogatePairSplitAcrossChunksIsEncodedIntact()
    {
        var terminal = new CaptureTerminal();

        // Fill a tiny first chunk so the emoji's high surrogate lands at the end of one chunk and
        // the low surrogate at the start of the next; the stateful Encoder must join them.
        var sb = new StringBuilder(4);
        sb.Append("abc");
        sb.Append('\ud83d'); // high surrogate of 🚀 — fills the 4-char chunk.
        sb.Append('\ude80'); // low surrogate — forced into a new chunk.
        sb.Append("end");

        Assert.IsTrue(CountChunks(sb) > 1, "test needs the pair to straddle chunks");

        await terminal.WriteAsync(sb);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(sb.ToString()), terminal.Captured.ToArray());
        StringAssert.Contains(Encoding.UTF8.GetString(terminal.Captured.ToArray()), "🚀");
    }

    [TestMethod]
    public async Task WholeFrameIsWrittenInOneCall()
    {
        var terminal = new CaptureTerminal();
        var sb = new StringBuilder();
        global::Schipper.Io.Ansi.Sequences.Ansi.BeginSync(sb);
        global::Schipper.Io.Ansi.Sequences.Ansi.CursorTo(sb, 1, 1);
        sb.Append("frame");
        global::Schipper.Io.Ansi.Sequences.Ansi.EndSync(sb);

        await terminal.WriteAsync(sb);

        Assert.AreEqual(1, terminal.WriteCalls);
    }

    private static int CountChunks(StringBuilder sb)
    {
        int count = 0;
        foreach (ReadOnlyMemory<char> _ in sb.GetChunks())
        {
            count++;
        }

        return count;
    }

    private sealed class CaptureTerminal : ITerminal
    {
        public List<byte> Captured { get; } = [];

        public int WriteCalls { get; private set; }

        public int Columns => 80;

        public int Rows => 25;

        public event Action? Resized { add { } remove { } }

        public ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            Captured.AddRange(bytes.ToArray());
            return ValueTask.CompletedTask;
        }

        public ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(0);
    }
}
