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
                DrawerStateParameters newParameters = new()
                {
                    Width = StateParameters.Width,
                    Height = StateParameters.Height
                };
                StateParameters = newParameters;

                DrawerNavigationStack = new();
            }

            NotifyStateChanged();
        }
    }
    private bool _isConfirmDrawerOpen { get; set; }
    public bool IsConfirmDrawerOpen
    {
        get { return _isConfirmDrawerOpen; }
        set
        {
            _isConfirmDrawerOpen = value;

            if (value == false)
            {
                DrawerStateParameters newParameters = new()
                {
                    Width = StateParameters.Width,
                    Height = StateParameters.Height
                };
                StateParameters = newParameters;

                DrawerNavigationStack = new();
            }

            NotifyStateChanged();
        }
    }

    public event Action? OnDrawerChange;

    public delegate Task ConfirmClickHandler();
    public event ConfirmClickHandler? OnConfirmClick;

    public void NotifyStateChanged() => OnDrawerChange?.Invoke();
    public void NotifyConfirmClicked() => OnConfirmClick?.Invoke();

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

    public void OpenDrawer(DrawerStateParameters drawerStateParameters)
    {
        StateParameters = drawerStateParameters;
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

    public void SwitchDrawerContent(Type content, string title = "", Dictionary<string, object> parameters = null)
    {
        StateParameters.Content = content;
        StateParameters.Title = title;
        StateParameters.ContentParameters = parameters ?? [];

        DrawerNavigationStack.Push(new(StateParameters));

        NotifyStateChanged();
    }

    public void CloseDrawer()
    {
        IsDrawerOpen = false;
        IsConfirmDrawerOpen = false;
    }

    public void OpenConfirmDrawer(string tittle, string description)
    {
        StateParameters.Title = tittle;
        StateParameters.description = description;

        IsConfirmDrawerOpen = true;
    }
}