using MudBlazor;

namespace MealWiz.Shared.Theme;

/// <summary>
/// The MudBlazor theme: palettes, typography and the default radius from the Visual redesign spec. MudBlazor only
/// covers part of the design; the rest are <c>--mw-*</c> CSS variables in <c>wwwroot/css/mealwiz.css</c>, keyed off
/// the <c>data-theme</c> attribute that <c>MainLayout</c> sets from <see cref="ModeAttribute"/>. Keep the hex values
/// here and in that stylesheet in sync; nothing else in the app uses hex colours (UI-02).
/// </summary>
public static class MealWizTheme
{
    // Fills: the same in both themes.
    private const string Orange = "#FF9F43";
    private const string Green = "#8FD45A";
    private const string Red = "#FF5C5C";
    private const string TextOnColor = "#1A140E";
    private const string DarkButton = "#1A140E";
    private const string DarkButtonText = "#F5EFE6";

    private static readonly string[] BodyFont = ["DM Sans", "system-ui", "sans-serif"];
    private static readonly string[] HeadingFont = ["Bricolage Grotesque", "DM Sans", "system-ui", "sans-serif"];

    /// <summary>The value of the <c>data-theme</c> attribute the custom CSS tokens key off.</summary>
    public static string ModeAttribute(bool isDarkMode) => isDarkMode ? "dark" : "light";

    public static MudTheme Build()
    {
        var theme = new MudTheme
        {
            PaletteLight = new PaletteLight
            {
                Primary = Orange,
                PrimaryContrastText = TextOnColor,
                Secondary = Green,
                SecondaryContrastText = TextOnColor,
                Tertiary = Green,
                TertiaryContrastText = TextOnColor,
                Success = Green,
                SuccessContrastText = TextOnColor,
                Error = Red,
                ErrorContrastText = TextOnColor,
                Info = "#1F5FAD",
                InfoContrastText = "#FFFFFF",
                Warning = "#8A5A00",
                WarningContrastText = "#FFFFFF",
                Dark = DarkButton,
                DarkContrastText = DarkButtonText,

                Background = "#FAF7F2",
                BackgroundGray = "#F1EBE2",
                Surface = "#FFFFFF",
                AppbarBackground = "#FFFFFF",
                AppbarText = "#1F1A15",
                DrawerBackground = "#FFFFFF",
                DrawerText = "#1F1A15",
                DrawerIcon = "#6B6359",

                TextPrimary = "#1F1A15",
                TextSecondary = "#6B6359",
                TextDisabled = "#A39A8F",
                ActionDefault = "#6B6359",
                ActionDisabled = "#A39A8F",
                ActionDisabledBackground = "#F1EBE2",
                Divider = "#F1EBE2",
                DividerLight = "#F1EBE2",
                LinesDefault = "#E8E0D5",
                LinesInputs = "#E8E0D5",
                TableLines = "#F1EBE2",
                TableStriped = "#FAF7F2",
                Skeleton = "#F1EBE2",
                OverlayDark = "rgba(40,28,16,0.45)",
            },
            PaletteDark = new PaletteDark
            {
                Primary = Orange,
                PrimaryContrastText = TextOnColor,
                Secondary = Green,
                SecondaryContrastText = TextOnColor,
                Tertiary = Green,
                TertiaryContrastText = TextOnColor,
                Success = Green,
                SuccessContrastText = TextOnColor,
                Error = Red,
                ErrorContrastText = TextOnColor,
                Info = "#7FB8FF",
                InfoContrastText = TextOnColor,
                Warning = "#FFC24B",
                WarningContrastText = TextOnColor,
                Dark = DarkButton,
                DarkContrastText = DarkButtonText,

                Background = "#14110E",
                BackgroundGray = "#2A2520",
                Surface = "#1F1B17",
                AppbarBackground = "#221E19",
                AppbarText = "#F5EFE6",
                DrawerBackground = "#1F1B17",
                DrawerText = "#F5EFE6",
                DrawerIcon = "#ABA397",

                TextPrimary = "#F5EFE6",
                TextSecondary = "#ABA397",
                TextDisabled = "#6E665C",
                ActionDefault = "#ABA397",
                ActionDisabled = "#6E665C",
                ActionDisabledBackground = "#2A2520",
                Divider = "#2A2520",
                DividerLight = "#2A2520",
                LinesDefault = "#2E2924",
                LinesInputs = "#2E2924",
                TableLines = "#2A2520",
                TableStriped = "#1F1B17",
                Skeleton = "#2A2520",
                OverlayDark = "rgba(8,6,4,0.72)",
            },
            LayoutProperties = new LayoutProperties
            {
                DefaultBorderRadius = "14px",
            },
        };

        // Mutate MudBlazor's defaults rather than replacing them, so the sizes and line heights stay.
        var typography = theme.Typography;
        typography.Default.FontFamily = BodyFont;
        BaseTypography[] headings = [typography.H1, typography.H2, typography.H3, typography.H4, typography.H5];
        foreach (var heading in headings)
        {
            heading.FontFamily = HeadingFont;
            heading.FontWeight = "800";
        }
        typography.H6.FontFamily = HeadingFont;
        typography.H6.FontWeight = "600";
        typography.Button.FontWeight = "700";
        typography.Button.TextTransform = "none";

        return theme;
    }
}
