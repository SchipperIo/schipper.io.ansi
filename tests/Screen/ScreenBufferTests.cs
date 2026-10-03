using Schipper.Io.Ansi.Screen;

namespace Schipper.Io.Ansi.Tests.Screen;

[TestClass]
public sealed class ScreenBufferTests
{
    [TestMethod]
    public void ConstructorRejectsAProductThatOverflows()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(static () => new ScreenBuffer(65536, 65536));
    }

    [TestMethod]
    public void ResizeRejectsAProductThatOverflows()
    {
        ScreenBuffer buffer = new(2, 2);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => buffer.Resize(65536, 65536));
    }
}
