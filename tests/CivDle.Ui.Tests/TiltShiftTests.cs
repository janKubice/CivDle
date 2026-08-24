using Xunit;
using CivDle.Rendering;

namespace CivDle.Ui.Tests;

/// <summary>
/// Testy počítání masky a pruhů. Samotné rozostření se testovat nedá bez
/// grafiky, ale to, co se u něj kazí — díry mezi pruhy a nespojitý přechod —
/// je čistá matematika a otestovat se dá.
/// </summary>
public sealed class TiltShiftTests
{
    [Fact]
    public void FocusBandIsSharp()
    {
        var options = TiltShiftOptions.Default;

        Assert.Equal(0f, options.BlurAt(options.FocusCenter));
        Assert.Equal(0f, options.BlurAt(options.FocusCenter + options.FocusHalfHeight * 0.99f));
        Assert.Equal(0f, options.BlurAt(options.FocusCenter - options.FocusHalfHeight * 0.99f));
    }

    [Fact]
    public void EdgesAreFullyBlurred()
    {
        var options = TiltShiftOptions.Default;

        Assert.Equal(options.Strength, options.BlurAt(0f), 3);
        Assert.Equal(options.Strength, options.BlurAt(1f), 3);
    }

    [Fact]
    public void BlurGrowsWithDistanceFromFocus()
    {
        var options = TiltShiftOptions.Default;
        float previous = -1f;

        // Od středu pásu nahoru: hodnota nesmí nikde klesnout, jinak by v přechodu
        // byl pruh, který je ostřejší než ten nad ním.
        for (float y = options.FocusCenter; y >= 0f; y -= 0.01f)
        {
            float blur = options.BlurAt(y);
            Assert.True(blur >= previous, $"na y={y:F2} rozostření kleslo z {previous:F3} na {blur:F3}");
            previous = blur;
        }
    }

    [Fact]
    public void TransitionHasNoJump()
    {
        var options = TiltShiftOptions.Default;
        float previous = options.BlurAt(0f);

        for (float y = 0.005f; y <= 1f; y += 0.005f)
        {
            float blur = options.BlurAt(y);
            Assert.True(Math.Abs(blur - previous) < 0.06f, $"skok na y={y:F3}");
            previous = blur;
        }
    }

    [Fact]
    public void StrengthZeroMeansNoBlurAnywhere()
    {
        var options = TiltShiftOptions.Default with { Strength = 0f };

        for (float y = 0f; y <= 1f; y += 0.05f)
        {
            Assert.Equal(0f, options.BlurAt(y));
        }

        Assert.False((TiltShiftOptions.Default with { Strength = 0f, Punch = 0f }).HasEffect);
    }

    [Theory]
    [InlineData(1080, 0)]
    [InlineData(1080, 137)]
    [InlineData(2160, 0)]
    [InlineData(67, 5)]
    public void StripsCoverTheWholeAreaWithoutGaps(int height, int top)
    {
        int cursor = top;

        for (int i = 0; i < TiltShift.StripCount; i++)
        {
            var (stripTop, stripBottom) = TiltShift.StripBounds(i, TiltShift.StripCount, top, height);
            Assert.Equal(cursor, stripTop);
            Assert.True(stripBottom >= stripTop);
            cursor = stripBottom;
        }

        Assert.Equal(top + height, cursor);
    }

    [Fact]
    public void SourceStripStaysInsideTheSmallTexture()
    {
        const int destinationHeight = 1080;
        const int sourceHeight = 135; // osminová zmenšenina

        for (int i = 0; i < TiltShift.StripCount; i++)
        {
            var (top, bottom) = TiltShift.StripBounds(i, TiltShift.StripCount, 0, destinationHeight);
            var slice = TiltShift.SourceStrip(top, bottom, 0, destinationHeight, 240, sourceHeight);

            Assert.True(slice.Y >= 0);
            Assert.True(slice.Height >= 1, "prázdný výřez by pruh vůbec nenakreslil");
            Assert.True(slice.Bottom <= sourceHeight);
        }
    }

    [Fact]
    public void SourceStripFollowsTheDestinationStrip()
    {
        // Pruh z dolní části obrázku musí sáhnout do dolní části zmenšeniny.
        var (top, bottom) = TiltShift.StripBounds(TiltShift.StripCount - 1, TiltShift.StripCount, 0, 800);
        var slice = TiltShift.SourceStrip(top, bottom, 0, 800, 100, 100);

        Assert.True(slice.Y > 90, $"výřez začíná na {slice.Y}, čekal se konec textury");
    }

    [Fact]
    public void GentlePresetBlursLessThanDefault()
    {
        Assert.True(TiltShiftOptions.Gentle.BlurAt(0f) < TiltShiftOptions.Default.BlurAt(0f));
        Assert.True(TiltShiftOptions.Gentle.HasEffect);
    }
}
