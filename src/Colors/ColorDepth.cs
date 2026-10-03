namespace Schipper.Io.Ansi.Colors;

/// <summary>
/// The color capability tiers a terminal may support, ordered from most to least constrained.
/// Used by <see cref="Color.Downgrade"/> and the depth-aware <see cref="ScreenRenderer"/> overloads
/// to map richer colors onto what the remote terminal can actually display.
/// </summary>
public enum ColorDepth
{
    /// <summary>The classic 16 ANSI colors (SGR 30-37/90-97 and 40-47/100-107).</summary>
    Basic16 = 0,

    /// <summary>The xterm 256-color palette (SGR 38;5;n / 48;5;n).</summary>
    Palette256 = 1,

    /// <summary>24-bit truecolor (SGR 38;2;r;g;b / 48;2;r;g;b).</summary>
    TrueColor = 2,
}
