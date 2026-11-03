using MealWiz.Shared.Theme;
using MudBlazor;
using MudBlazor.Utilities;
using Xunit;

namespace MealWiz.Shared.Tests.Theme.UnitTests;

/// <summary>
/// Pins the spec's colour roles and typography (project "Visual redesign", Visual system). Fills are the same in both
/// themes; the per-theme text shades live in <c>css/mealwiz.css</c>, not in the palette.
/// </summary>
public class MealWizThemeTests
{
    private readonly MudTheme _theme = MealWizTheme.Build();

    [Theory]
    [InlineData(false, "light")]
    [InlineData(true, "dark")]
    public void ModeAttribute_names_the_mode_the_css_tokens_key_off(bool isDarkMode, string expected)
    {
        Assert.Equal(expected, MealWizTheme.ModeAttribute(isDarkMode));
    }

    [Fact]
    public void Role_fills_and_the_text_on_them_are_the_same_in_both_palettes()
    {
        Palette[] palettes = [_theme.PaletteLight, _theme.PaletteDark];

        foreach (var palette in palettes)
        {
            Assert.Equal("#ff9f43ff", Hex(palette.Primary));
            Assert.Equal("#8fd45aff", Hex(palette.Secondary));
            Assert.Equal("#ff5c5cff", Hex(palette.Error));
            Assert.Equal("#1a140eff", Hex(palette.PrimaryContrastText));
            Assert.Equal("#1a140eff", Hex(palette.SecondaryContrastText));
        }
    }

    [Theory]
    [InlineData(false, "#faf7f2ff", "#ffffffff", "#1f1a15ff", "#6b6359ff")]
    [InlineData(true, "#14110eff", "#1f1b17ff", "#f5efe6ff", "#aba397ff")]
    public void Palettes_use_the_spec_tokens(bool dark, string background, string surface, string text, string muted)
    {
        Palette palette = dark ? _theme.PaletteDark : _theme.PaletteLight;

        Assert.Equal(background, Hex(palette.Background));
        Assert.Equal(surface, Hex(palette.Surface));
        Assert.Equal(text, Hex(palette.TextPrimary));
        Assert.Equal(muted, Hex(palette.TextSecondary));
    }

    [Fact]
    public void Body_text_uses_DM_Sans_and_headings_use_Bricolage_Grotesque()
    {
        var typography = _theme.Typography;

        Assert.Equal("DM Sans", typography.Default.FontFamily![0]);
        BaseTypography[] headings = [typography.H1, typography.H2, typography.H3, typography.H4, typography.H5, typography.H6];
        Assert.All(headings, h => Assert.Equal("Bricolage Grotesque", h.FontFamily![0]));
    }

    private static string Hex(MudColor color) => color.ToString(MudColorOutputFormats.HexA).ToLowerInvariant();
}
