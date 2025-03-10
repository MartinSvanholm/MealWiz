namespace Features.Services.DrawerStateContainer;

public partial class DrawerStateContainer : IDrawerStateContainer
{
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
    }
}