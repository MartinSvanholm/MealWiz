using FluentResults;
using MealWiz.Shared.Features.GroceryList.AddGroceryItem;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItem;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Features.GroceryList.State;
using MealWiz.Shared.Features.GroceryList.UpdateGroceryItem;
using MediatR;
using Moq;
using MudBlazor;
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
