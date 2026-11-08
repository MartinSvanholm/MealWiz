using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.State;
using MealWiz.Shared.Services.DrawerStateContainer;
using Microsoft.Extensions.Localization;

namespace MealWiz.Shared.Features.GroceryList.DeleteGroceryItem;

/// <summary>
/// The confirm drawer both ways of deleting an item open: the edit drawer's red header action and swiping a row.
/// </summary>
public static class ConfirmDeleteGroceryItem
{
    public static void Open(IDrawerStateContainer drawerStateContainer, IGroceryListStateContainer groceryListStateContainer,
        IStringLocalizer<GroceryListResource> localizer, GroceryItem groceryItem)
    {
        drawerStateContainer.OpenConfirmDrawer(localizer["GroceryItem_DeleteConfirmTitle", groceryItem.Name],
            localizer["GroceryItem_DeleteConfirmMessage"], async () =>
        {
            await groceryListStateContainer.DeleteGroceryItem(groceryItem);
            drawerStateContainer.CloseDrawer();
        });
    }
}
