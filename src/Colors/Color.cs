using System.Text;

namespace Schipper.Io.Ansi.Colors;

/// <summary>How a <see cref="Color"/> value should be interpreted.</summary>
public enum ColorKind : byte
{
    /// <summary>The terminal's default foreground/background (SGR 39/49).</summary>
    Default = 0,

    /// <summary>One of the 16 <see cref="BasicColor"/> values.</summary>
    Basic = 1,

    /// <summary>An index into the xterm 256-color palette.</summary>
    Palette256 = 2,

    /// <summary>A 24-bit truecolor RGB value.</summary>
    TrueColor = 3,
}

/// <summary>
/// An immutable terminal color that can be the default color, one of the 16 basic colors,
/// a 256-palette index, or a 24-bit RGB value. Knows how to emit its own SGR parameters.
/// </summary>
public readonly struct Color : IEquatable<Color>
{
    private readonly byte _a; // basic index / palette index / red
    private readonly byte _g;
    private readonly byte _b;

    private Color(ColorKind kind, byte a, byte g, byte b)
    {
        Kind = kind;
        _a = a;
        _g = g;
        _b = b;
    }

    public ColorKind Kind { get; }

    /// <summary>The terminal default color.</summary>
    public static Color Default => new(ColorKind.Default, 0, 0, 0);

    /// <summary>A color from the 16-color basic set.</summary>
    public static Color Basic(BasicColor color) => new(ColorKind.Basic, (byte)color, 0, 0);

    /// <summary>A color from the xterm 256-color palette.</summary>
    public static Color Palette(byte index) => new(ColorKind.Palette256, index, 0, 0);

    /// <summary>A 24-bit truecolor value.</summary>
    public static Color Rgb(byte r, byte g, byte b) => new(ColorKind.TrueColor, r, g, b);

    /// <summary>The basic color index (valid only when <see cref="Kind"/> is <see cref="ColorKind.Basic"/>).</summary>
    public BasicColor AsBasic => (BasicColor)_a;

    /// <summary>The palette index (valid only when <see cref="Kind"/> is <see cref="ColorKind.Palette256"/>).</summary>
    public byte PaletteIndex => _a;

    public byte R => _a;
    public byte G => _g;
    public byte B => _b;

    /// <summary>Appends the SGR parameters that select this color as a foreground or background.</summary>
    public void AppendSgr(StringBuilder sb, bool foreground)
    {
        switch (Kind)
        {
            case ColorKind.Default:
                sb.Append(foreground ? "39" : "49");
                break;

            case ColorKind.Basic:
            {
                int idx = _a & 0x0F;
                int code = idx < 8
                    ? (foreground ? 30 : 40) + idx
                    : (foreground ? 90 : 100) + (idx - 8);
                sb.Append(code);
                break;
            }

            case ColorKind.Palette256:
                sb.Append(foreground ? "38;5;" : "48;5;").Append(_a);
                break;

            case ColorKind.TrueColor:
                sb.Append(foreground ? "38;2;" : "48;2;")
                  .Append(_a).Append(';').Append(_g).Append(';').Append(_b);
                break;
        }
    }

    /// <summary>
    /// Returns the closest color representable at <paramref name="depth"/>. Colors that already fit
    /// the target depth — and <see cref="ColorKind.Default"/>, which every terminal understands —
    /// pass through unchanged. Truecolor downgraded to <see cref="ColorDepth.Palette256"/> maps to
    /// the nearest xterm-256 entry (6×6×6 color cube or grayscale ramp, minimal squared RGB
    /// distance, exact matches preferred); anything downgraded to <see cref="ColorDepth.Basic16"/>
    /// maps to the nearest of the 16 basic colors against the standard xterm palette.
    /// </summary>
    public Color Downgrade(ColorDepth depth) => Kind switch
    {
        ColorKind.Palette256 when depth == ColorDepth.Basic16 => Basic((BasicColor)Palette256ToBasic[_a]),
        ColorKind.TrueColor when depth == ColorDepth.Basic16 => Basic((BasicColor)NearestBasic(_a, _g, _b)),
        ColorKind.TrueColor when depth == ColorDepth.Palette256 => Palette(NearestPalette256(_a, _g, _b)),
        _ => this,
    };

    /// <summary>
    /// RGB values of the 16 basic colors in the standard xterm palette, three bytes per color.
    /// </summary>
    private static ReadOnlySpan<byte> BasicRgb =>
    [
        0, 0, 0, //          0 Black
        205, 0, 0, //        1 Red
        0, 205, 0, //        2 Green
        205, 205, 0, //      3 Yellow
        0, 0, 238, //        4 Blue
        205, 0, 205, //      5 Magenta
        0, 205, 205, //      6 Cyan
        229, 229, 229, //    7 White
        127, 127, 127, //    8 BrightBlack
        255, 0, 0, //        9 BrightRed
        0, 255, 0, //       10 BrightGreen
        255, 255, 0, //     11 BrightYellow
        92, 92, 255, //     12 BrightBlue
        255, 0, 255, //     13 BrightMagenta
        0, 255, 255, //     14 BrightCyan
        255, 255, 255, //   15 BrightWhite
    ];

    /// <summary>Precomputed 256-palette-index → nearest-basic-color table (indices 0-15 pass through).</summary>
    private static readonly byte[] Palette256ToBasic = BuildPalette256ToBasic();

    private static byte[] BuildPalette256ToBasic()
    {
        byte[] table = new byte[256];
        for (int i = 0; i < 16; i++)
        {
            table[i] = (byte)i;
        }

        for (int i = 16; i < 232; i++)
        {
            int c = i - 16;
            table[i] = NearestBasic(CubeLevel(c / 36), CubeLevel((c / 6) % 6), CubeLevel(c % 6));
        }

        for (int i = 232; i < 256; i++)
        {
            int gray = 8 + (10 * (i - 232));
            table[i] = NearestBasic(gray, gray, gray);
        }

        return table;
    }

    private static byte NearestBasic(int r, int g, int b)
    {
        ReadOnlySpan<byte> palette = BasicRgb;
        int best = 0;
        int bestDist = int.MaxValue;
        for (int i = 0; i < 16; i++)
        {
            int dist = SquaredDistance(r, g, b, palette[i * 3], palette[(i * 3) + 1], palette[(i * 3) + 2]);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return (byte)best;
    }

    private static byte NearestPalette256(int r, int g, int b)
    {
        // Candidate from the 6x6x6 cube (levels 0, 95, 135, 175, 215, 255).
        int ri = CubeIndex(r);
        int gi = CubeIndex(g);
        int bi = CubeIndex(b);
        int cubeDist = SquaredDistance(r, g, b, CubeLevel(ri), CubeLevel(gi), CubeLevel(bi));
        byte cube = (byte)(16 + (36 * ri) + (6 * gi) + bi);
        if (cubeDist == 0)
        {
            return cube;
        }

        // Candidate from the grayscale ramp (232-255: values 8 + 10n). The nearest gray to any RGB
        // minimizes the squared distance at the component mean.
        int mean = (r + g + b) / 3;
        int grayIndex = Math.Clamp((mean - 3) / 10, 0, 23);
        int gray = 8 + (10 * grayIndex);
        int grayDist = SquaredDistance(r, g, b, gray, gray, gray);
        return grayDist < cubeDist ? (byte)(232 + grayIndex) : cube;
    }

    /// <summary>Index of the nearest 6x6x6 cube level (0, 95, 135, 175, 215, 255) for one component.</summary>
    private static int CubeIndex(int c) => c < 48 ? 0 : c < 115 ? 1 : (c - 35) / 40;

    private static int CubeLevel(int index) => index == 0 ? 0 : 55 + (40 * index);

    private static int SquaredDistance(int r, int g, int b, int r2, int g2, int b2)
    {
        int dr = r - r2;
        int dg = g - g2;
        int db = b - b2;
        return (dr * dr) + (dg * dg) + (db * db);
    }

    public bool Equals(Color other) =>
        Kind == other.Kind && _a == other._a && _g == other._g && _b == other._b;

    public override bool Equals(object? obj) => obj is Color c && Equals(c);

    public override int GetHashCode() => HashCode.Combine((byte)Kind, _a, _g, _b);

    public static bool operator ==(Color left, Color right) => left.Equals(right);

    public static bool operator !=(Color left, Color right) => !left.Equals(right);
}
