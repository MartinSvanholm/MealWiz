using Microsoft.AspNetCore.Components;

namespace MealWiz.Shared.Services.DrawerStateContainer;

public class DrawerStateParameters
{
    public DrawerStateParameters(DrawerStateParameters drawerStateParameters)
    {
        Content = drawerStateParameters.Content;
        ContentParameters = drawerStateParameters.ContentParameters;
        Header = drawerStateParameters.Header;
        Title = drawerStateParameters.Title;
        Width = drawerStateParameters.Width;
        Height = drawerStateParameters.Height;
    }

    public DrawerStateParameters()
    {

    }

    public Type Content { get; set; }
    public Dictionary<string, object> ContentParameters { get; set; }
    public RenderFragment Header { get; set; }
    public string Title { get; set; }
    public string description { get; set; }
    public string Width { get; set; } = null;
    public string Height { get; set; } = "100%";
}