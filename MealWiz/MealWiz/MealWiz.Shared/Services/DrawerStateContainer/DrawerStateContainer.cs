using Microsoft.AspNetCore.Components;

namespace MealWiz.Shared.Services.DrawerStateContainer;

public partial class DrawerStateContainer : IDrawerStateContainer
{
    public DrawerStateParameters StateParameters { get; set; } = new DrawerStateParameters();
    public Stack<DrawerStateParameters> DrawerNavigationStack { get; set; } = new Stack<DrawerStateParameters>();
    private bool _isDrawerOpen { get; set; }
    public bool IsDrawerOpen
    {
        get { return _isDrawerOpen; }
        set
        {
            _isDrawerOpen = value;

            if (value == false)
            {
                StateParameters = new();
                DrawerNavigationStack = new();
            }

            NotifyStateChanged();
        }
    }

    public event Action? OnDrawerChange;

    public void NotifyStateChanged() => OnDrawerChange?.Invoke();

    public void OpenDrawer(string title, Type content)
    {
        StateParameters.Title = title;
        StateParameters.Content = content;
        IsDrawerOpen = true;
    }

    public void OpenDrawer(RenderFragment header, Type content)
    {
        StateParameters.Header = header;
        StateParameters.Content = content;
        IsDrawerOpen = true;
    }

    public void NavigateBack()
    {
        if (DrawerNavigationStack.Count > 1)
        {
            DrawerNavigationStack.Pop();

            StateParameters.Content = DrawerNavigationStack.Peek().Content;
            StateParameters.ContentParameters = DrawerNavigationStack.Peek().ContentParameters;
            StateParameters.Header = DrawerNavigationStack.Peek().Header;
            StateParameters.Title = DrawerNavigationStack.Peek().Title;
            StateParameters.Width = DrawerNavigationStack.Peek().Width;

            NotifyStateChanged();
        }
    }
}