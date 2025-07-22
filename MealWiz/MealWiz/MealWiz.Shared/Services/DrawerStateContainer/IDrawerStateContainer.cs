using Microsoft.AspNetCore.Components;
using static MealWiz.Shared.Services.DrawerStateContainer.DrawerStateContainer;

namespace MealWiz.Shared.Services.DrawerStateContainer;

public interface IDrawerStateContainer
{
    Stack<DrawerStateParameters> DrawerNavigationStack { get; set; }
    bool IsDrawerOpen { get; set; }
    bool IsConfirmDrawerOpen { get; set; }
    DrawerStateParameters StateParameters { get; set; }

    event Action? OnDrawerChange;

    event ConfirmClickHandler? OnConfirmClick;

    void CloseDrawer();
    void NavigateBack();
    void NotifyStateChanged();
    void NotifyConfirmClicked();
    void OpenDrawer(RenderFragment header, Type content);
    void OpenDrawer(string title, Type content);
    void SwitchDrawerContent(Type content, string title = "", Dictionary<string, object> parameters = null);
    void OpenConfirmDrawer(string title, string description);
}