using System.Text.Json;
using Blazored.LocalStorage;
using FluentResults;
using TheMealWorkshop.Shared.Features.GroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.AddGroceryCategory;
using TheMealWorkshop.Shared.Features.GroceryCategories.ApplyLearnedSectionOrder;
using TheMealWorkshop.Shared.Features.GroceryList.Models;
using TheMealWorkshop.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.UpdateGroceryCategory;
using TheMealWorkshop.Shared.Tests.TestInfrastructure;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryListSettings;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetRememberedGroceryNames;
using TheMealWorkshop.Shared.Features.GroceryCategories.RememberGroceryName;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Features.GroceryCategories.State;
using MediatR;
using Moq;
using MudBlazor;
using Xunit;

namespace TheMealWorkshop.Shared.Tests.Features.GroceryCategories.UnitTests.State;

public class GroceryCategoriesStateContainerTests
{
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<ISnackbar> _snackbar = new();
    private readonly Mock<ILocalStorageService> _localStorage = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 15, 20, 0, 0, TimeSpan.Zero));
    private readonly GroceryCategoriesStateContainer _container;
    private int _notifications;

    public GroceryCategoriesStateContainerTests()
    {
        _container = new GroceryCategoriesStateContainer(_mediator.Object, _localStorage.Object, _time) { CurrentSnackbar = _snackbar.Object };
        _container.OnStateChanged += () => _notifications++;
        SetupNames(Result.Ok(new List<RememberedGroceryName>()));
        SetupSettings(Result.Ok(new GroceryListSettings()));
    }

    private void SetupSettings(Result<GroceryListSettings> result) =>
        _mediator.Setup(m => m.Send(It.IsAny<GetGroceryListSettings.Query>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

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

    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<GroceryCategoriesResource> Localizer =
        TestLocalizer.For<GroceryCategoriesResource>();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ValidateName_Empty_IsRequired(string? name)
    {
        SetupGet(Result.Ok(Defaults()));
        await _container.LoadCategories();

        Assert.Equal("Give the category a name", _container.ValidateName(name, null, Localizer));
    }

    [Theory]
    [InlineData("dairy & EGGS ")]
    [InlineData("Other")]
    public async Task ValidateName_AnotherCategorysName_IsTaken_IgnoringCaseAndSpaces(string name)
    {
        SetupGet(Result.Ok(Defaults()));
        await _container.LoadCategories();

        Assert.Equal("A category with that name already exists", _container.ValidateName(name, null, Localizer));
    }

    [Fact]
    public async Task ValidateName_ItsOwnName_OrANewOne_IsFine()
    {
        SetupGet(Result.Ok(Defaults()));
        await _container.LoadCategories();

        Assert.Null(_container.ValidateName("Dairy & eggs", 5, Localizer));
        Assert.Null(_container.ValidateName("Health food", null, Localizer));
    }

    [Fact]
    public async Task ValidateName_ComparesWithTheNamesShownInTheAppLanguage()
    {
        SetupGet(Result.Ok(Defaults()));
        await _container.LoadCategories();
        using var _ = CultureScope.Use("da");

        Assert.NotNull(_container.ValidateName("mejeri & æg", null, Localizer));
        Assert.Null(_container.ValidateName("Dairy & eggs", null, Localizer));
    }

    [Fact]
    public async Task AddCategory_Success_ReloadsTheCategories()
    {
        SetupGet(Result.Ok(Defaults()));
        _mediator.Setup(m => m.Send(It.IsAny<AddGroceryCategory.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok(new GroceryCategory { Id = 12, Name = "Health food", Position = 11 }));

        Assert.True(await _container.AddCategory("Health food", "🥗"));

        var command = (AddGroceryCategory.Command)_mediator.Invocations.Single(i => i.Arguments[0] is AddGroceryCategory.Command).Arguments[0];
        Assert.Equal(("Health food", "🥗"), (command.Name, command.Emoji));
        Assert.Single(_mediator.Invocations, i => i.Arguments[0] is GetGroceryCategories.Query);
    }

    [Fact]
    public async Task UpdateCategory_Failure_ShowsTheError_AndReloadsNothing()
    {
        _mediator.Setup(m => m.Send(It.IsAny<UpdateGroceryCategory.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail<GroceryCategory>("A category with that name already exists"));

        Assert.False(await _container.UpdateCategory(3, "Bakery", null));

        Assert.DoesNotContain(_mediator.Invocations, i => i.Arguments[0] is GetGroceryCategories.Query);
        _snackbar.Verify(s => s.Add("A category with that name already exists", It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()));
    }

    [Fact]
    public async Task LoadCategories_AlsoLoadsTheSettings()
    {
        SetupGet(Result.Ok(Defaults()));
        var learnedUntil = new DateTime(2026, 11, 14, 18, 0, 0, DateTimeKind.Utc);
        SetupSettings(Result.Ok(new GroceryListSettings(SectionOrderMode.Manual, learnedUntil)));

        await _container.LoadCategories();

        Assert.Equal(new GroceryListSettings(SectionOrderMode.Manual, learnedUntil), _container.Settings);
    }

    [Fact]
    public async Task LoadCategories_SettingsFail_KeepsTheCategoriesAndLearnedMode()
    {
        SetupGet(Result.Ok(Defaults()));
        SetupSettings(Result.Fail<GroceryListSettings>("No response from server"));

        await _container.LoadCategories();

        Assert.Equal(11, _container.Categories.Count);
        Assert.Equal(SectionOrderMode.Learned, _container.Settings.OrderMode);
    }

    // The clock is 20:00; these ticks end at 10:20, so their trip is over.
    private static GroceryItem Ticked(string name, int minute) =>
        new() { Name = name, IsPicked = true, PickedAt = new DateTime(2026, 11, 15, 10, minute, 0, DateTimeKind.Utc) };

    private void SetupApply(Result result) =>
        _mediator.Setup(m => m.Send(It.IsAny<ApplyLearnedSectionOrder.Command>(), It.IsAny<CancellationToken>())).ReturnsAsync(result);

    private List<ApplyLearnedSectionOrder.Command> ApplyCalls =>
        _mediator.Invocations.Select(i => i.Arguments.FirstOrDefault()).OfType<ApplyLearnedSectionOrder.Command>().ToList();

    [Fact]
    public async Task LearnFromTicks_AFinishedTrip_LearnsFromEachCategorysFirstTick_AndRemembersWhereItGot()
    {
        SetupGet(Result.Ok(Defaults()));
        SetupApply(Result.Ok());
        await _container.LoadCategories();

        // Dairy first (milk), then bakery; the second milk tick doesn't count.
        await _container.LearnFromTicks([Ticked("milk", 0), Ticked("bread", 10), Ticked("cheese", 20)], basketCleared: false);

        var command = Assert.Single(ApplyCalls);
        Assert.Equal(new DateTime(2026, 11, 15, 10, 20, 0, DateTimeKind.Utc), command.LearnedUntil);
        Assert.Equal(11, command.Categories.Count);
        var dairy = command.Categories.Single(c => c.CategoryId == 5);
        var bakery = command.Categories.Single(c => c.CategoryId == 2);
        Assert.True(dairy.Score < 5 && bakery.Score > 2);
        Assert.Equal(command.LearnedUntil, _container.Settings.LearnedUntil);
        Assert.Equal(dairy.Score, _container.Categories.Single(c => c.Id == 5).OrderScore);
    }

    [Fact]
    public async Task LearnFromTicks_NothingFinished_SendsNothing()
    {
        SetupGet(Result.Ok(Defaults()));
        await _container.LoadCategories();
        var lastTick = new DateTime(2026, 11, 15, 19, 0, 0, DateTimeKind.Utc);

        await _container.LearnFromTicks(
            [new GroceryItem { Name = "milk", IsPicked = true, PickedAt = lastTick.AddMinutes(-10) },
             new GroceryItem { Name = "bread", IsPicked = true, PickedAt = lastTick }],
            basketCleared: false);

        Assert.Empty(ApplyCalls);
    }

    [Fact]
    public async Task LearnFromTicks_InManualMode_OnlyMovesLearnedUntil()
    {
        SetupGet(Result.Ok(Defaults()));
        SetupSettings(Result.Ok(new GroceryListSettings(SectionOrderMode.Manual)));
        SetupApply(Result.Ok());
        await _container.LoadCategories();

        await _container.LearnFromTicks([Ticked("milk", 0), Ticked("bread", 10)], basketCleared: true);

        var command = Assert.Single(ApplyCalls);
        Assert.Empty(command.Categories);
        Assert.Equal(new DateTime(2026, 11, 15, 10, 10, 0, DateTimeKind.Utc), _container.Settings.LearnedUntil);
    }

    [Fact]
    public async Task LearnFromTicks_AFailedWrite_ShowsTheError_AndChangesNothing()
    {
        SetupGet(Result.Ok(Defaults()));
        SetupApply(Result.Fail("No response from server"));
        await _container.LoadCategories();

        await _container.LearnFromTicks([Ticked("milk", 0), Ticked("bread", 10)], basketCleared: false);

        Assert.Null(_container.Settings.LearnedUntil);
        Assert.All(_container.Categories, c => Assert.Null(c.OrderScore));
        _snackbar.Verify(s => s.Add("No response from server", It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()));
    }
}
