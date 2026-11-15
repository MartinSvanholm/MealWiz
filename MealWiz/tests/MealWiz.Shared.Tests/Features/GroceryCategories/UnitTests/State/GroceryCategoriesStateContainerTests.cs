using System.Text.Json;
using Blazored.LocalStorage;
using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.GetRememberedGroceryNames;
using MealWiz.Shared.Features.GroceryCategories.RememberGroceryName;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Features.GroceryCategories.State;
using MediatR;
using Moq;
using MudBlazor;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryCategories.UnitTests.State;

public class GroceryCategoriesStateContainerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly Mock<ILocalStorageService> _localStorage = new();
    private readonly GroceryCategoriesStateContainer _container;
    private int _notifications;

    public GroceryCategoriesStateContainerTests()
    {
        _container = new GroceryCategoriesStateContainer(_mediator.Object, _localStorage.Object) { CurrentSnackbar = _snackbar.Object };
        _container.OnStateChanged += () => _notifications++;
        SetupNames(Result.Ok(new List<RememberedGroceryName>()));
    }

    private void SetupNames(Result<List<RememberedGroceryName>> result) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetRememberedGroceryNames.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

    private void SetupRemember(Result result) =>
        _mediator.Setup(m => m.Send(It.IsAny<RememberGroceryName.Command>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

    private List<RememberGroceryName.Command> RememberCalls =>
        _mediator.Invocations.Select(i => i.Arguments.FirstOrDefault()).OfType<RememberGroceryName.Command>().ToList();

    internal static List<GroceryCategory> Defaults() =>
        DefaultCategoryKeyExtensions.All.Select(k => GroceryCategory.Default(k) is var c ? WithId(c, c.Position) : c).ToList();

    private static GroceryCategory WithId(GroceryCategory category, int id)
    {
        category.Id = id;
        return category;
    }

    private void SetupGet(Result<List<GroceryCategory>> result) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetGroceryCategories.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

    private void SetupCreate(Result<List<GroceryCategory>> result) =>
        _mediator.Setup(m => m.Send(It.IsAny<CreateDefaultGroceryCategories.Command>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

    private int CreateCalls => _mediator.Invocations.Count(i => i.Arguments.FirstOrDefault() is CreateDefaultGroceryCategories.Command);

    [Fact]
    public async Task LoadCategories_UserHasCategories_KeepsThemAndCreatesNothing()
    {
        var existing = Defaults().Take(3).ToList();
        SetupGet(Result.Ok(existing));

        await _container.LoadCategories();

        Assert.Equal(existing, _container.Categories);
        Assert.Equal(0, CreateCalls);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task LoadCategories_UserHasNone_CreatesTheDefaults()
    {
        SetupGet(Result.Ok(new List<GroceryCategory>()));
        SetupCreate(Result.Ok(Defaults()));

        await _container.LoadCategories();

        Assert.Equal(1, CreateCalls);
        Assert.Equal(11, _container.Categories.Count);
        Assert.True(_container.Categories[^1].IsOther);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task LoadCategories_LoadFails_CreatesNothingAndKeepsTheListEmpty()
    {
        SetupGet(Result.Fail<List<GroceryCategory>>("No response from server"));

        await _container.LoadCategories();

        Assert.Empty(_container.Categories);
        Assert.Equal(0, CreateCalls);
        Assert.Equal(0, _notifications);
    }

    [Fact]
    public async Task LoadCategories_CreateFails_KeepsTheListEmpty()
    {
        SetupGet(Result.Ok(new List<GroceryCategory>()));
        SetupCreate(Result.Fail<List<GroceryCategory>>("No response from server"));

        await _container.LoadCategories();

        Assert.Empty(_container.Categories);
        Assert.Equal(0, _notifications);
    }

    [Fact]
    public async Task CategoryFor_PlacesANameInTheLoadedCategories()
    {
        SetupGet(Result.Ok(Defaults()));
        await _container.LoadCategories();

        Assert.Equal(DefaultCategoryKey.DairyEggs, _container.CategoryFor("mælk")?.DefaultKey);
        Assert.True(_container.CategoryFor("flimflam")?.IsOther);
    }

    [Fact]
    public void CategoryFor_BeforeTheCategoriesLoad_IsNull() =>
        Assert.Null(_container.CategoryFor("milk"));

    private void SetupStoredFolded(List<int>? ids) =>
        _localStorage.Setup(l => l.GetItemAsync<List<int>>(GroceryCategoriesStateContainer.FoldedStorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ids);

    [Fact]
    public async Task ToggleFolded_FoldsAndUnfolds_AndSavesTheFoldedIds()
    {
        await _container.ToggleFolded(5);
        await _container.ToggleFolded(2);

        Assert.True(_container.IsFolded(5));
        Assert.True(_container.IsFolded(2));
        _localStorage.Verify(l => l.SetItemAsync(GroceryCategoriesStateContainer.FoldedStorageKey,
            It.Is<List<int>>(ids => ids.SequenceEqual(new[] { 2, 5 })), It.IsAny<CancellationToken>()));

        await _container.ToggleFolded(5);

        Assert.False(_container.IsFolded(5));
        _localStorage.Verify(l => l.SetItemAsync(GroceryCategoriesStateContainer.FoldedStorageKey,
            It.Is<List<int>>(ids => ids.SequenceEqual(new[] { 2 })), It.IsAny<CancellationToken>()));
        Assert.Equal(3, _notifications);
    }

    [Fact]
    public async Task LoadFolded_RestoresTheStoredSections()
    {
        SetupStoredFolded([3, 7]);

        await _container.LoadFolded();

        Assert.True(_container.IsFolded(3));
        Assert.True(_container.IsFolded(7));
        Assert.False(_container.IsFolded(1));
    }

    [Fact]
    public async Task LoadFolded_NothingStored_FoldsNothing()
    {
        SetupStoredFolded(null);

        await _container.LoadFolded();

        Assert.False(_container.IsFolded(1));
    }

    [Fact]
    public async Task LoadFolded_UnreadableValue_FoldsNothing()
    {
        await _container.ToggleFolded(4);
        _localStorage.Setup(l => l.GetItemAsync<List<int>>(GroceryCategoriesStateContainer.FoldedStorageKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new JsonException("not a list"));

        await _container.LoadFolded();

        Assert.False(_container.IsFolded(4));
    }

    [Fact]
    public async Task LoadCategories_AlsoLoadsTheRememberedNames_AndTheyBeatTheDictionary()
    {
        SetupGet(Result.Ok(Defaults()));
        SetupNames(Result.Ok(new List<RememberedGroceryName> { new("milk", 6) }));

        await _container.LoadCategories();

        Assert.Equal(6, _container.RememberedNames["milk"]);
        Assert.Equal(DefaultCategoryKey.Frozen, _container.CategoryFor("  MILK ")?.DefaultKey);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public async Task LoadCategories_RememberedNamesFail_KeepsTheCategories()
    {
        SetupGet(Result.Ok(Defaults()));
        SetupNames(Result.Fail<List<RememberedGroceryName>>("No response from server"));

        await _container.LoadCategories();

        Assert.Equal(11, _container.Categories.Count);
        Assert.Empty(_container.RememberedNames);
        _snackbar.Verify(s => s.Add("No response from server", It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()));
    }

    [Fact]
    public async Task MoveName_RemembersTheName_AndItResolvesThereEverywhere()
    {
        SetupGet(Result.Ok(Defaults()));
        SetupRemember(Result.Ok());
        await _container.LoadCategories();

        var moved = await _container.MoveName("Tofu", 3);

        Assert.True(moved);
        var command = Assert.Single(RememberCalls);
        Assert.Equal(("Tofu", 3), (command.Name, command.CategoryId));
        Assert.Equal(DefaultCategoryKey.MeatFish, _container.CategoryFor("tofu")?.DefaultKey);
        Assert.Equal(2, _notifications);
    }

    [Fact]
    public async Task MoveName_ToTheCategoryItAlreadyResolvesTo_SendsNothing()
    {
        SetupGet(Result.Ok(Defaults()));
        await _container.LoadCategories();

        Assert.True(await _container.MoveName("milk", 5));

        Assert.Empty(RememberCalls);
    }

    [Fact]
    public async Task MoveName_Fails_LeavesTheNameWhereItWas()
    {
        SetupGet(Result.Ok(Defaults()));
        SetupRemember(Result.Fail("No response from server"));
        await _container.LoadCategories();

        var moved = await _container.MoveName("tofu", 3);

        Assert.False(moved);
        Assert.True(_container.CategoryFor("tofu")?.IsOther);
        Assert.Equal(1, _notifications);
    }
}
