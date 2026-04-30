using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.GroceryList.State;

public interface IGroceryListStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    Models.GroceryList GroceryList { get; }

    event Action OnStateChanged;

    Task AddGroceryItemToGroceryList(GroceryItem groceryItem);
    Task LoadGroceryItems();
    void NotifyStateChanged();
    Task UpdateGroceryItem(GroceryItem groceryItem);
    Task ToggleIsPicked(GroceryItem item);
    Task DeleteGroceryItem(GroceryItem groceryItem);
}

public class GroceryListStateContainer(
    IMediator mediator) : StateContainerBase, IGroceryListStateContainer
{
    private Models.GroceryList groceryList { get; set; } = new();
    public Models.GroceryList GroceryList => groceryList;

    public async Task LoadGroceryItems()
    {
        var result = await mediator.Send(new GetAllGroceryItems.GetAllGroceryItems.Query());
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        groceryList.Items = result.Value.OrderBy(item => item.IsPicked).ToList();

        NotifyStateChanged();
    }

    public async Task AddGroceryItemToGroceryList(GroceryItem groceryItem)
    {
        var result = await mediator.Send(new AddGroceryItem.AddGroceryItem.Command(groceryItem));
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        groceryItem.Id = result.Value;

        groceryList.Items.Add(groceryItem);
        groceryList.SortItemsByIsPicked();

        NotifyStateChanged();
    }

    public async Task UpdateGroceryItem(GroceryItem groceryItem)
    {
        var result = await mediator.Send(new UpdateGroceryItem.UpdateGroceryItem.Command(groceryItem));
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        NotifyStateChanged();
    }

    public async Task ToggleIsPicked(GroceryItem item)
    {
        item.IsPicked = !item.IsPicked;

        var result = await mediator.Send(new UpdateGroceryItem.UpdateGroceryItem.Command(item));
        if (result.Handle(CurrentSnackbar).IsFailed)
        {
            item.IsPicked = !item.IsPicked;
            return;
        }

        GroceryList.SortItemsByIsPicked();

        NotifyStateChanged();
    }

    public async Task DeleteGroceryItem(GroceryItem groceryItem)
    {
        var result = await mediator.Send(new DeleteGroceryItem.DeleteGroceryItem.Command(groceryItem));
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        GroceryList.Items.Remove(groceryItem);
        NotifyStateChanged();
    }
}