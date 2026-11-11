using FluentResults;
using MealWiz.Shared.Features.GroceryList.AddGroceryItem;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItem;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.GetGroceryItemSources;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.State;
using MealWiz.Shared.Features.GroceryList.UnlinkGroceryItems;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MediatR;
using Moq;
using MudBlazor;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.UnitTests.State;

public class GroceryListStateContainerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly GroceryListStateContainer _container;
    private int _notifications;

    public GroceryListStateContainerTests()
    {
        _container = new GroceryListStateContainer(_mediator.Object) { CurrentSnackbar = _snackbar.Object };
        _container.OnStateChanged += () => _notifications++;
    }

    [Fact]
    public async Task LoadGroceryItems_Success_ListsUnpickedItemsFirst()
    {
        SetupLoad(Item(1, "a", picked: true), Item(2, "b"), Item(3, "c", picked: true), Item(4, "d"));

        await _container.LoadGroceryItems();

        Assert.Equal(new[] { "b", "d", "a", "c" }, Names());
        Assert.Equal(1, _notifications);
        VerifyNoToast();
    }

    [Fact]
    public async Task LoadGroceryItems_Failure_ShowsAnErrorAndKeepsTheList()
    {
        await LoadItems(Item(1, "milk"));
        _mediator
            .Setup(m => m.Send(It.IsAny<GetAllGroceryItems.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<List<GroceryItem>>("Load failed"));

        await _container.LoadGroceryItems();

        Assert.Equal(new[] { "milk" }, Names());
        Assert.Equal(0, _notifications);
        VerifyToast("Load failed", Severity.Error);
    }

    [Fact]
    public async Task AddGroceryItemToGroceryList_Success_AddsTheSavedRowBeforePickedItems()
    {
        await LoadItems(Item(1, "picked", picked: true));
        var owner = Guid.NewGuid();
        var saved = new GroceryItem { Id = 42, Name = "milk", CreatedBy = owner };
        _mediator
            .Setup(m => m.Send(It.IsAny<AddGroceryItem.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(saved));

        await _container.AddGroceryItemToGroceryList(new GroceryItem { Name = "milk" });

        // The saved row has its owner, so it can be ticked or edited before a reload (MEA-42)
        Assert.Same(saved, _container.GroceryList.Items[0]);
        Assert.Equal(new[] { "milk", "picked" }, Names());
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task AddGroceryItemToGroceryList_Failure_ShowsAnErrorAndDoesNotAddIt()
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<AddGroceryItem.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<GroceryItem>("Add failed"));
        var item = new GroceryItem { Name = "milk" };

        await _container.AddGroceryItemToGroceryList(item);

        Assert.Empty(_container.GroceryList.Items);
        Assert.Equal(0, _notifications);
        VerifyToast("Add failed", Severity.Error);
    }

    [Fact]
    public async Task UpdateGroceryItem_Success_SendsTheItemAndNotifies()
    {
        var item = Item(1, "milk");
        SetupUpdate(Result.Ok(1));

        await _container.UpdateGroceryItem(item);

        _mediator.Verify(m => m.Send(It.Is<UpdateGroceryItem.Command>(c => c.GroceryItem == item), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task UpdateGroceryItem_Failure_ShowsAnErrorWithoutNotifying()
    {
        SetupUpdate(Result.Fail<int>("Update failed"));

        await _container.UpdateGroceryItem(Item(1, "milk"));

        Assert.Equal(0, _notifications);
        VerifyToast("Update failed", Severity.Error);
    }

    // MEA-76: the edit drawer saves a copy of the item, so the list shows the copy once the update succeeds.
    [Fact]
    public async Task UpdateGroceryItem_Success_PutsTheSavedCopyInTheList()
    {
        var milk = Item(1, "milk");
        var bread = Item(2, "bread");
        _container.GroceryList.Items.AddRange([milk, bread]);
        var edited = Item(1, "oat milk");
        SetupUpdate(Result.Ok(1));

        await _container.UpdateGroceryItem(edited);

        Assert.Equal([edited, bread], _container.GroceryList.Items);
    }

    [Fact]
    public async Task UpdateGroceryItem_Failure_KeepsTheListedItem()
    {
        var milk = Item(1, "milk");
        _container.GroceryList.Items.Add(milk);
        SetupUpdate(Result.Fail<int>("Update failed"));

        await _container.UpdateGroceryItem(Item(1, "oat milk"));

        Assert.Same(milk, Assert.Single(_container.GroceryList.Items));
        Assert.Equal("milk", milk.Name);
    }

    [Fact]
    public async Task ToggleIsPicked_Success_SavesTheFlippedFlagAndMovesTheItemLast()
    {
        var milk = Item(1, "milk");
        await LoadItems(milk, Item(2, "bread"));
        bool? sentIsPicked = null;
        _mediator
            .Setup(m => m.Send(It.IsAny<UpdateGroceryItem.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateGroceryItem.Command c, CancellationToken _) =>
            {
                sentIsPicked = c.GroceryItem.IsPicked;
                return Result.Ok(1);
            });

        await _container.ToggleIsPicked(milk);

        Assert.True(sentIsPicked);
        Assert.True(milk.IsPicked);
        Assert.Equal(new[] { "bread", "milk" }, Names());
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task ToggleIsPicked_Failure_RevertsTheFlag()
    {
        var milk = Item(1, "milk", picked: true);
        await LoadItems(milk);
        SetupUpdate(Result.Fail<int>("Update failed"));

        await _container.ToggleIsPicked(milk);

        Assert.True(milk.IsPicked);
        Assert.Equal(0, _notifications);
        VerifyToast("Update failed", Severity.Error);
    }

    [Fact]
    public async Task DeleteGroceryItem_Success_RemovesTheItem()
    {
        var milk = Item(1, "milk");
        await LoadItems(milk, Item(2, "bread"));
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteGroceryItem.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(1));

        await _container.DeleteGroceryItem(milk);

        Assert.Equal(new[] { "bread" }, Names());
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task DeleteGroceryItem_Failure_ShowsAnErrorAndKeepsTheItem()
    {
        var milk = Item(1, "milk");
        await LoadItems(milk);
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteGroceryItem.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<int>("Delete failed"));

        await _container.DeleteGroceryItem(milk);

        Assert.Equal(new[] { "milk" }, Names());
        Assert.Equal(0, _notifications);
        VerifyToast("Delete failed", Severity.Error);
    }

    [Fact]
    public async Task ClearBasket_Success_DeletesThePickedItemsAndKeepsTheRest()
    {
        await LoadItems(Item(1, "milk"), Item(2, "bread", picked: true), Item(3, "eggs", picked: true));
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        await _container.ClearBasket();

        _mediator.Verify(m => m.Send(It.Is<DeleteGroceryItems.Command>(c => c.GroceryItemIds.Order().SequenceEqual(new[] { 2, 3 })), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new[] { "milk" }, Names());
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task ClearBasket_Failure_ShowsAnErrorAndKeepsTheBasket()
    {
        await LoadItems(Item(1, "milk"), Item(2, "bread", picked: true));
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail("Clear failed"));

        await _container.ClearBasket();

        Assert.Equal(new[] { "milk", "bread" }, Names());
        Assert.Equal(0, _notifications);
        VerifyToast("Clear failed", Severity.Error);
    }

    [Fact]
    public async Task ClearBasket_Success_AlsoDropsTheItemsSources()
    {
        await LoadItems(Item(1, "milk"), Item(2, "bread", picked: true));
        await LoadSourcesAsync(TestData.Source(1, 10, 20), TestData.Source(2, 10, 21));
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        await _container.ClearBasket();

        Assert.Equal([1], _container.Sources.Select(s => s.GroceryItemId));
    }

    [Fact]
    public async Task DeleteGroceryItem_Success_AlsoDropsItsSources()
    {
        var milk = Item(1, "milk");
        await LoadItems(milk, Item(2, "bread"));
        await LoadSourcesAsync(TestData.Source(1, 10, 20), TestData.Source(2, 10, 21));
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteGroceryItem.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(1));

        await _container.DeleteGroceryItem(milk);

        Assert.Equal([2], _container.Sources.Select(s => s.GroceryItemId));
    }

    [Fact]
    public async Task LoadSources_Success_StoresThemAndNotifies()
    {
        await LoadSourcesAsync(TestData.Source(1, 10, 20));

        Assert.Single(_container.Sources);
    }

    [Fact]
    public async Task LoadSources_Failure_ShowsTheErrorAndKeepsTheSources()
    {
        await LoadSourcesAsync(TestData.Source(1, 10, 20));
        _mediator
            .Setup(m => m.Send(It.IsAny<GetGroceryItemSources.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<List<GroceryItemSource>>("Load failed"));

        await _container.LoadSources();

        Assert.Single(_container.Sources);
        Assert.Equal(0, _notifications);
        VerifyToast("Load failed", Severity.Error);
    }

    [Fact]
    public async Task StaleItems_ReflectsTheLoadedItemsAndSources()
    {
        await LoadItems(TestData.LinkedGroceryItem("Flour", 1), TestData.LinkedGroceryItem("Leeks", 2), Item(3, "Milk"));
        await LoadSourcesAsync(TestData.StaleSource(1, 1, "Tacos"), TestData.CurrentSource(2, 2), TestData.StaleSource(3, 3, "Tacos"));

        Assert.Equal([1], _container.StaleItems.Select(i => i.Id));
    }

    [Fact]
    public async Task StaleItems_AfterTickingAnItemOff_LeavesItOut()
    {
        var flour = TestData.LinkedGroceryItem("Flour", 1);
        await LoadItems(flour);
        await LoadSourcesAsync(TestData.StaleSource(1, 1, "Tacos"));
        SetupUpdate(Result.Ok(1));

        await _container.ToggleIsPicked(flour);

        Assert.Empty(_container.StaleItems);
    }

    [Fact]
    public async Task DeleteAndUnlinkGroceryItems_Success_UnlinksThenDeletes()
    {
        await LoadItems(TestData.LinkedGroceryItem("Flour", 1), TestData.LinkedGroceryItem("Salsa", 2), TestData.LinkedGroceryItem("Leeks", 3));
        await LoadSourcesAsync(TestData.StaleSource(1, 1, "T"), TestData.StaleSource(2, 2, "T"), TestData.CurrentSource(3, 3));
        var order = new List<string>();
        SetupUnlink(Result.Ok(), order);
        SetupDelete(Result.Ok(), order);

        var ok = await _container.DeleteAndUnlinkGroceryItems([1], [2]);

        Assert.True(ok);
        Assert.Equal(["unlink", "delete"], order);
        Assert.Equal(["Salsa", "Leeks"], Names());
        Assert.True(_container.GroceryList.Items.Single(i => i.Id == 2).IsManual);
        Assert.Equal([3], _container.Sources.Select(s => s.GroceryItemId));
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task DeleteAndUnlinkGroceryItems_UnlinkFails_SendsNoDeleteAndChangesNothing()
    {
        await LoadItems(TestData.LinkedGroceryItem("Flour", 1), TestData.LinkedGroceryItem("Salsa", 2));
        await LoadSourcesAsync(TestData.StaleSource(1, 1, "T"), TestData.StaleSource(2, 2, "T"));
        SetupUnlink(Result.Fail("Unlink failed"));
        SetupDelete(Result.Ok());

        var ok = await _container.DeleteAndUnlinkGroceryItems([1], [2]);

        Assert.False(ok);
        _mediator.Verify(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(["Flour", "Salsa"], Names());
        Assert.False(_container.GroceryList.Items.Single(i => i.Id == 2).IsManual);
        Assert.Equal(2, _container.Sources.Count);
        Assert.Equal(0, _notifications);
        VerifyToast("Unlink failed", Severity.Error);
    }

    [Fact]
    public async Task DeleteAndUnlinkGroceryItems_DeleteFails_KeepsTheItemsButAppliesTheUnlink()
    {
        await LoadItems(TestData.LinkedGroceryItem("Flour", 1), TestData.LinkedGroceryItem("Salsa", 2));
        await LoadSourcesAsync(TestData.StaleSource(1, 1, "T"), TestData.StaleSource(2, 2, "T"));
        SetupUnlink(Result.Ok());
        SetupDelete(Result.Fail("Delete failed"));

        var ok = await _container.DeleteAndUnlinkGroceryItems([1], [2]);

        Assert.False(ok);
        Assert.Equal(["Flour", "Salsa"], Names());
        Assert.True(_container.GroceryList.Items.Single(i => i.Id == 2).IsManual);
        Assert.Equal([1], _container.Sources.Select(s => s.GroceryItemId));
        Assert.Equal(1, _notifications);
        VerifyToast("Delete failed", Severity.Error);
    }

    [Fact]
    public async Task DeleteAndUnlinkGroceryItems_EmptyLists_SkipThoseRequests()
    {
        await LoadItems(TestData.LinkedGroceryItem("Flour", 1), TestData.LinkedGroceryItem("Salsa", 2));
        await LoadSourcesAsync(TestData.StaleSource(1, 1, "T"), TestData.StaleSource(2, 2, "T"));

        Assert.True(await _container.DeleteAndUnlinkGroceryItems([], []));
        _mediator.Verify(m => m.Send(It.IsAny<UnlinkGroceryItems.Command>(), It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>()), Times.Never);

        SetupUnlink(Result.Ok());
        Assert.True(await _container.DeleteAndUnlinkGroceryItems([], [1, 2]));
        _mediator.Verify(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>()), Times.Never);

        SetupDelete(Result.Ok());
        Assert.True(await _container.DeleteAndUnlinkGroceryItems([1], []));
        _mediator.Verify(m => m.Send(It.IsAny<UnlinkGroceryItems.Command>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupUnlink(Result result, List<string>? order = null) =>
        _mediator
            .Setup(m => m.Send(It.IsAny<UnlinkGroceryItems.Command>(), It.IsAny<CancellationToken>()))
            .Callback(() => order?.Add("unlink"))
            .ReturnsAsync(result);

    private void SetupDelete(Result result, List<string>? order = null) =>
        _mediator
            .Setup(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>()))
            .Callback(() => order?.Add("delete"))
            .ReturnsAsync(result);

    private async Task LoadSourcesAsync(params GroceryItemSource[] sources)
    {
        _mediator
            .Setup(m => m.Send(It.IsAny<GetGroceryItemSources.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(sources.ToList()));
        await _container.LoadSources();
        _notifications = 0;
    }

    // ---- A deleted meal's or ingredient's items (MEA-93) ----

    private static GroceryItemSource LinkedSource(int id, int itemId, int mealId = 5, int ingredientId = 100) =>
        new() { Id = id, GroceryItemId = itemId, PlannedMealId = 500 + id, IngredientId = ingredientId, IngredientMealId = mealId, PlannedDayMealId = mealId };

    /// <summary>An item added from the meal plan (not manual).</summary>
    private static GroceryItem PlanItem(int id, string name, bool picked = false) => new() { Id = id, Name = name, IsPicked = picked, IsManual = false };

    private void SetupSources(params GroceryItemSource[] sources) =>
        _mediator
            .Setup(m => m.Send(It.IsAny<GetGroceryItemSources.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(sources.ToList()));

    private List<object> SentRequests() =>
        _mediator.Invocations.Where(i => i.Method.Name == nameof(IMediator.Send)).Select(i => i.Arguments[0]).ToList();

    [Fact]
    public async Task LoadItemsLinkedToMeal_Success_SetsItemsAndSourcesAndReturnsTheSort()
    {
        SetupLoad(PlanItem(1, "rice", picked: true), PlanItem(2, "beans"));
        SetupSources(LinkedSource(11, 1), LinkedSource(12, 2));

        var links = ResultAssert.Ok(await _container.LoadItemsLinkedToMeal(5));

        Assert.Equal([2], links.ToBuy.Select(i => i.Id));
        Assert.Equal([1], links.InBasket.Select(i => i.Id));
        Assert.Equal(new[] { "beans", "rice" }, Names());
        Assert.Equal(2, _container.Sources.Count);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task LoadItemsLinkedToMeal_SourcesFail_ReturnsTheFailureWithoutAToast()
    {
        SetupLoad(Item(2, "beans"));
        _mediator
            .Setup(m => m.Send(It.IsAny<GetGroceryItemSources.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<List<GroceryItemSource>>("Sources broke"));

        var result = await _container.LoadItemsLinkedToMeal(5);

        Assert.True(result.IsFailed);
        Assert.Empty(_container.GroceryList.Items);
        Assert.Equal(0, _notifications);
        VerifyNoToast();
    }

    [Fact]
    public async Task LoadItemsLinkedToIngredient_Success_SortsByIngredient()
    {
        SetupLoad(PlanItem(1, "rice"), PlanItem(2, "beans"));
        SetupSources(LinkedSource(11, 1, ingredientId: 100), LinkedSource(12, 2, ingredientId: 101));

        var links = ResultAssert.Ok(await _container.LoadItemsLinkedToIngredient(101));

        Assert.Equal([2], links.ToBuy.Select(i => i.Id));
    }

    [Fact]
    public async Task SettleLinkedItems_SendsDeleteThenUnlinkWithTheBasketThenDropSources()
    {
        _mediator.Setup(m => m.Send(It.IsAny<IRequest<Result>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        var links = new LinkedGroceryItems([Item(1, "a")], [Item(2, "b", picked: true)], [31]);

        ResultAssert.Ok(await _container.SettleLinkedItems(links, [1], [3]));

        var sent = SentRequests();
        Assert.Equal([typeof(DeleteGroceryItems.Command), typeof(UnlinkGroceryItems.Command), typeof(DeleteGroceryItemSources.Command)], sent.Select(r => r.GetType()));
        Assert.Equal([1], ((DeleteGroceryItems.Command)sent[0]).GroceryItemIds);
        Assert.Equal([3, 2], ((UnlinkGroceryItems.Command)sent[1]).GroceryItemIds);
        Assert.Equal([31], ((DeleteGroceryItemSources.Command)sent[2]).SourceIds);
    }

    [Fact]
    public async Task SettleLinkedItems_NothingToDo_SendsNothing()
    {
        ResultAssert.Ok(await _container.SettleLinkedItems(LinkedGroceryItems.Empty, [], []));

        Assert.Empty(SentRequests());
        Assert.Equal(0, _notifications);
    }

    [Fact]
    public async Task SettleLinkedItems_DeleteFails_StopsAndReturnsIt()
    {
        _mediator.Setup(m => m.Send(It.IsAny<DeleteGroceryItems.Command>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Fail("Delete broke"));
        var links = new LinkedGroceryItems([Item(1, "a")], [Item(2, "b", picked: true)], [31]);

        var result = await _container.SettleLinkedItems(links, [1], []);

        Assert.True(result.IsFailed);
        Assert.Single(SentRequests());
        VerifyNoToast();
    }

    [Fact]
    public async Task SettleLinkedItems_Success_UpdatesItemsAndSources()
    {
        SetupLoad(PlanItem(1, "a"), PlanItem(2, "b", picked: true), PlanItem(3, "c"), PlanItem(4, "d"));
        SetupSources(LinkedSource(11, 1), LinkedSource(12, 2), LinkedSource(13, 3), LinkedSource(31, 4), LinkedSource(32, 4, mealId: 6));
        await _container.LoadItemsLinkedToMeal(5);
        _notifications = 0;
        _mediator.Setup(m => m.Send(It.IsAny<IRequest<Result>>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok());
        var basket = _container.GroceryList.Items.Single(i => i.Id == 2);
        var links = new LinkedGroceryItems([], [basket], [31]);

        ResultAssert.Ok(await _container.SettleLinkedItems(links, [1], [3]));

        Assert.DoesNotContain(_container.GroceryList.Items, i => i.Id == 1);
        Assert.True(_container.GroceryList.Items.Single(i => i.Id == 2).IsManual);
        Assert.True(_container.GroceryList.Items.Single(i => i.Id == 3).IsManual);
        Assert.Equal([32], _container.Sources.Select(s => s.Id));
        Assert.Equal(1, _notifications);
    }

    private static GroceryItem Item(int id, string name, bool picked = false) => new() { Id = id, Name = name, IsPicked = picked };

    private IEnumerable<string> Names() => _container.GroceryList.Items.Select(i => i.Name);

    private void SetupLoad(params GroceryItem[] items) =>
        _mediator
            .Setup(m => m.Send(It.IsAny<GetAllGroceryItems.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(items.ToList()));

    private void SetupUpdate(Result<int> result) =>
        _mediator
            .Setup(m => m.Send(It.IsAny<UpdateGroceryItem.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    /// <summary>Puts <paramref name="items"/> on the list through LoadGroceryItems, then resets the notification count.</summary>
    private async Task LoadItems(params GroceryItem[] items)
    {
        SetupLoad(items);
        await _container.LoadGroceryItems();
        _notifications = 0;
    }

    private void VerifyToast(string message, Severity severity) =>
        _snackbar.Verify(s => s.Add(message, severity, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);

    private void VerifyNoToast() =>
        _snackbar.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Never);
}
