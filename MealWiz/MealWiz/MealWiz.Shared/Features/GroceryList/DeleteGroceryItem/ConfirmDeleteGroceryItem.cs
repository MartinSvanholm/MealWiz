using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.State;
using MealWiz.Shared.Services.DrawerStateContainer;

namespace MealWiz.Shared.Features.GroceryList.DeleteGroceryItem;

/// <summary>
/// The confirm drawer both ways of deleting an item open: the edit drawer's red header action and swiping a row.
/// </summary>
public static class ConfirmDeleteGroceryItem
{
    public static void Open(IDrawerStateContainer drawerStateContainer, IGroceryListStateContainer groceryListStateContainer, GroceryItem groceryItem)
    {
        drawerStateContainer.OpenConfirmDrawer($"Delete {groceryItem.Name}?", "Are you sure you want to delete this grocery item?", async () =>
        {
            await groceryListStateContainer.DeleteGroceryItem(groceryItem);
            drawerStateContainer.CloseDrawer();
        });
    }
}
