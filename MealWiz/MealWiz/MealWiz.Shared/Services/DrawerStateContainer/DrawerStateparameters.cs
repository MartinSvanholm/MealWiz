using Microsoft.AspNetCore.Components;

namespace MealWiz.Shared.Services.DrawerStateContainer;

public class DrawerStateParameters
{
    public DrawerStateParameters(DrawerStateParameters drawerStateParameters)
    {
        Content = drawerStateParameters.Content;
        ContentParameters = drawerStateParameters.ContentParameters;
        Header = drawerStateParameters.Header;
        HeaderAction = drawerStateParameters.HeaderAction;
        Title = drawerStateParameters.Title;
        Width = drawerStateParameters.Width;
        Height = drawerStateParameters.Height;
    }

    public DrawerStateParameters()
    {

    }

    /// <summary>
    /// The height of every drawer that holds input fields: the full screen minus a 64px strip at the top, so the page
    /// behind still shows it's a sheet. The fields sit at the top and the DrawerFooter button at the bottom.
    /// </summary>
    public const string FormHeight = "calc(100% - 64px)";

    public Type Content { get; set; }
    public Dictionary<string, object> ContentParameters { get; set; }
    /// <summary>Replaces the whole header row (close button and title). Prefer <see cref="Title"/> and <see cref="HeaderAction"/>.</summary>
    public RenderFragment Header { get; set; }
    /// <summary>An optional action on the right of the header row, e.g. a red delete button.</summary>
    public RenderFragment HeaderAction { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Width { get; set; } = null;
    public string Height { get; set; } = "100%";
}