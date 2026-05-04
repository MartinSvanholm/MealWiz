using MudBlazor;

namespace MealWiz.Shared.Theme;

public static class MealWizTheme
{
    public static MudTheme Build() => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#5678B8",
            PrimaryLighten = "#7A98D0",
            PrimaryDarken = "#3F5C95",
            Secondary = "#6FB089",
            Tertiary = "#E8A87C",
            Success = "#6BAA75",

            Background = "#FAF8F5",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#2C2A28",
            DrawerBackground = "#F4F1EC",
            DrawerText = "#2C2A28",
            DrawerIcon = "#5C5854",

            TextPrimary = "#2C2A28",
            TextSecondary = "#6B6764",
            TextDisabled = "#A8A6A2",
            ActionDefault = "#5C5854",
            ActionDisabled = "#C0BFBC",
            ActionDisabledBackground = "#EDEAE4",
            Divider = "#E8E3DC",
            DividerLight = "#F0EDE7",
            LinesDefault = "#E8E3DC",
            LinesInputs = "#D6D1C8",
            TableLines = "#E8E3DC",
            TableStriped = "#F4F1EC",
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#7A98D0",
            PrimaryLighten = "#9CB3DD",
            PrimaryDarken = "#5678B8",
            Secondary = "#8FE0B0",
            Tertiary = "#EDB89A",
            Success = "#7CB987",

            Background = "#1B1D22",
            Surface = "#24272D",
            AppbarBackground = "#1F2127",
            AppbarText = "#E4E4E7",
            DrawerBackground = "#1F2127",
            DrawerText = "#E4E4E7",
            DrawerIcon = "#C0BFBC",

            TextPrimary = "#E4E4E7",
            TextSecondary = "#A8A6A2",
            TextDisabled = "#6B6764",
            ActionDefault = "#C0BFBC",
            ActionDisabled = "#5C5854",
            ActionDisabledBackground = "#2F3138",
            Divider = "#2F3138",
            DividerLight = "#272A30",
            LinesDefault = "#2F3138",
            LinesInputs = "#3A3D44",
            TableLines = "#2F3138",
            TableStriped = "#24272D",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
        },
    };
}
