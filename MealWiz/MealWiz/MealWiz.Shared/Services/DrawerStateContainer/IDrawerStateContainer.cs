using Microsoft.AspNetCore.Components;

namespace MealWiz.Shared.Services.DrawerStateContainer;

public interface IDrawerStateContainer
{
    Stack<DrawerStateParameters> DrawerNavigationStack { get; set; }
    bool IsDrawerOpen { get; set; }
    bool IsConfirmDrawerOpen { get; set; }
    DrawerStateParameters StateParameters { get; set; }

    event Action? OnDrawerChange;

    void CloseDrawer();
    void NavigateBack();
    void NotifyStateChanged();
    Task NotifyConfirmClickedAsync();
    void OpenDrawer(RenderFragment header, Type content);
    void OpenDrawer(string title, Type content);
    void OpenDrawer(DrawerStateParameters drawerStateParameters);
    void SwitchDrawerContent(Type content, string title = "", Dictionary<string, object> parameters = null);
    void OpenConfirmDrawer(string title, string description, Func<Task> onConfirm);
}
