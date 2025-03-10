using Microsoft.AspNetCore.Components;

namespace MealWizFeatures.Services.DrawerStateContainer
{
    public interface IDrawerStateContainer
    {
        Stack<DrawerStateParameters> DrawerNavigationStack { get; set; }
        bool IsDrawerOpen { get; set; }
        DrawerStateParameters StateParameters { get; set; }

        event Action? OnDrawerChange;

        void CloseDrawer();
        void NavigateBack();
        void NotifyStateChanged();
        void OpenDrawer(RenderFragment header, Type content);
        void OpenDrawer(string title, Type content);
        void SwitchDrawerContent(Type content, string title = "", Dictionary<string, object> parameters = null);
    }
}