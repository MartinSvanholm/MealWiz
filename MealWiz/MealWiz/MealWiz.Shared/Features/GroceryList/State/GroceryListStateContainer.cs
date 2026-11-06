using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;

namespace MealWiz.Shared.Features.GroceryList.State;

public interface IGroceryListStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }
    Models.GroceryList GroceryList { get; }
    /// <summary>Which planned day and ingredient each item came from; loaded by <see cref="LoadSources"/>.</summary>
    List<GroceryItemSource> Sources { get; }

    event Action OnStateChanged;

    Task AddGroceryItemToGroceryList(GroceryItem groceryItem);
    Task LoadGroceryItems();
    Task LoadSources();
    void NotifyStateChanged();
    Task UpdateGroceryItem(GroceryItem groceryItem);
    Task ToggleIsPicked(GroceryItem item);
    Task DeleteGroceryItem(GroceryItem groceryItem);
    /// <summary>Deletes every picked item ("Clear" next to "In the basket").</summary>
    Task ClearBasket();
    /// <summary>Items from meals no longer in the plan; computed from the loaded items and sources.</summary>
    IReadOnlyList<GroceryItem> StaleItems { get; }
    /// <summary>
    /// Review clean-up: unlinks <paramref name="unlinkIds"/> (they stay as manual items), then deletes
    /// <paramref name="deleteIds"/>. False when a request failed (the toast is shown).
    /// </summary>
    Task<bool> DeleteAndUnlinkGroceryItems(List<int> deleteIds, List<int> unlinkIds);
}

public class GroceryListStateContainer(
    IMediator mediator) : StateContainerBase, IGroceryListStateContainer
{
    private Models.GroceryList groceryList { get; set; } = new();
    public Models.GroceryList GroceryList => groceryList;

    public List<GroceryItemSource> Sources { get; private set; } = [];

    public async Task LoadSources()
    {
        var result = await mediator.Send(new GetGroceryItemSources.GetGroceryItemSources.Query());
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        Sources = result.Value;
        NotifyStateChanged();
    }

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

        // Keep the saved row, not the item we sent: it has the owner and timestamps an update needs
        groceryList.Items.Add(result.Value);
        groceryList.SortItemsByIsPicked();

        NotifyStateChanged();
    }

    public async Task UpdateGroceryItem(GroceryItem groceryItem)
    {
        var result = await mediator.Send(new UpdateGroceryItem.UpdateGroceryItem.Command(groceryItem));
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        // The edit drawer saves a copy of the listed item, so swap the saved copy in (MEA-76)
        var index = groceryList.Items.FindIndex(item => item.Id == groceryItem.Id);
        if (index >= 0) groceryList.Items[index] = groceryItem;

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
        Sources.RemoveAll(source => source.GroceryItemId == groceryItem.Id);
        NotifyStateChanged();
    }

    public async Task ClearBasket()
    {
        var picked = GroceryList.Items.Where(item => item.IsPicked).ToList();

        var result = await mediator.Send(new DeleteGroceryItems.DeleteGroceryItems.Command(picked.ConvertAll(item => item.Id)));
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        GroceryList.Items.RemoveAll(picked.Contains);
        Sources.RemoveAll(source => picked.Any(item => item.Id == source.GroceryItemId));
        NotifyStateChanged();
    }

    public IReadOnlyList<GroceryItem> StaleItems => StaleGroceryItems.Find(GroceryList.Items, Sources);

    public async Task<bool> DeleteAndUnlinkGroceryItems(List<int> deleteIds, List<int> unlinkIds)
    {
        var changed = false;

        if (unlinkIds.Count > 0)
        {
            var unlinked = await mediator.Send(new UnlinkGroceryItems.UnlinkGroceryItems.Command(unlinkIds));
            if (unlinked.Handle(CurrentSnackbar).IsFailed) return false;

            foreach (var item in GroceryList.Items.Where(item => unlinkIds.Contains(item.Id))) item.IsManual = true;
            Sources.RemoveAll(source => unlinkIds.Contains(source.GroceryItemId));
            changed = true;
        }

        if (deleteIds.Count > 0)
        {
            var deleted = await mediator.Send(new DeleteGroceryItems.DeleteGroceryItems.Command(deleteIds));
            if (deleted.Handle(CurrentSnackbar).IsFailed)
            {
                if (changed) NotifyStateChanged();
                return false;
            }

            GroceryList.Items.RemoveAll(item => deleteIds.Contains(item.Id));
            Sources.RemoveAll(source => deleteIds.Contains(source.GroceryItemId));
            changed = true;
        }

        if (changed) NotifyStateChanged();
        return true;
    }
}