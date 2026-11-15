using System.Text.Json;
using Blazored.LocalStorage;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using MudBlazor;
using MealWiz.Shared.Features.GroceryList.Models;
using ApplyLearnedSectionOrderAction = MealWiz.Shared.Features.GroceryCategories.ApplyLearnedSectionOrder.ApplyLearnedSectionOrder;
using AddGroceryCategoryAction = MealWiz.Shared.Features.GroceryCategories.AddGroceryCategory.AddGroceryCategory;
using DeleteGroceryCategoryAction = MealWiz.Shared.Features.GroceryCategories.DeleteGroceryCategory.DeleteGroceryCategory;
using UpdateGroceryCategoryAction = MealWiz.Shared.Features.GroceryCategories.UpdateGroceryCategory.UpdateGroceryCategory;
using CreateDefaultGroceryCategoriesAction = MealWiz.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories.CreateDefaultGroceryCategories;
using GetGroceryCategoriesAction = MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories.GetGroceryCategories;
using GetGroceryListSettingsAction = MealWiz.Shared.Features.GroceryCategories.GetGroceryListSettings.GetGroceryListSettings;
using GetRememberedGroceryNamesAction = MealWiz.Shared.Features.GroceryCategories.GetRememberedGroceryNames.GetRememberedGroceryNames;
using RememberGroceryNameAction = MealWiz.Shared.Features.GroceryCategories.RememberGroceryName.RememberGroceryName;

namespace MealWiz.Shared.Features.GroceryCategories.State;

public interface IGroceryCategoriesStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }

    /// <summary>The user's categories by position; empty until <see cref="LoadCategories"/> succeeds.</summary>
    IReadOnlyList<GroceryCategory> Categories { get; }

    event Action OnStateChanged;

    /// <summary>The account's grocery list settings (section order mode, what was learned); loaded with the categories.</summary>
    GroceryListSettings Settings { get; }

    /// <summary>Normalised grocery name → the category the user put it in; loaded with the categories.</summary>
    IReadOnlyDictionary<string, int> RememberedNames { get; }

    /// <summary>
    /// Loads the user's categories, creating the eleven defaults the first time (when the user has none), and the
    /// remembered names and the settings. A failed remembered-names or settings load shows its error and keeps the
    /// categories (and the default settings).
    /// </summary>
    Task LoadCategories();

    /// <summary>The user's category for a grocery name (<see cref="CategoryResolver.Resolve"/>); null before the categories load.</summary>
    GroceryCategory? CategoryFor(string? name);

    /// <summary>
    /// Puts a name in a category everywhere (remembered). True when the name now resolves there, including when it
    /// already did (nothing is sent). On failure the error shows and nothing changes.
    /// </summary>
    Task<bool> MoveName(string name, int categoryId);

    /// <summary>Adds a category just above Other, then reloads; false when it failed (the error is shown).</summary>
    Task<bool> AddCategory(string name, string? emoji);

    /// <summary>Renames a category and sets its emoji, then reloads; false when it failed (the error is shown).</summary>
    Task<bool> UpdateCategory(int id, string name, string? emoji);

    /// <summary>
    /// Deletes a category, handing its names and dictionary words to <paramref name="moveToId"/>, then reloads; false
    /// when it failed (the error is shown).
    /// </summary>
    Task<bool> DeleteCategory(int id, int moveToId);

    /// <summary>
    /// Why <paramref name="name"/> can't be a category's name, or null: it is empty, or another category than
    /// <paramref name="categoryId"/> already shows that name in the current language (ignoring case and spaces).
    /// </summary>
    string? ValidateName(string? name, int? categoryId, IStringLocalizer<GroceryCategoriesResource> localizer);

    /// <summary>
    /// Learns the section order from the finished shopping trips among <paramref name="picked"/> (ticks after
    /// <see cref="GroceryListSettings.LearnedUntil"/>; a trip is finished two hours after its last tick, or when the
    /// basket is cleared). In manual mode it only records that those ticks were seen. Nothing finished: nothing sent.
    /// A failed write shows its error and changes nothing.
    /// </summary>
    Task LearnFromTicks(IReadOnlyList<GroceryItem> picked, bool basketCleared);

    /// <summary>Whether a section is folded on the grocery list (heading and count only). Kept on this device.</summary>
    bool IsFolded(int categoryId);

    /// <summary>Folds or unfolds a section and saves the folded sections to local storage.</summary>
    Task ToggleFolded(int categoryId);

    /// <summary>Reads the folded sections from local storage; an unreadable value folds nothing.</summary>
    Task LoadFolded();
}

public class GroceryCategoriesStateContainer(IMediator mediator, ILocalStorageService localStorage, TimeProvider time)
    : StateContainerBase, IGroceryCategoriesStateContainer
{
    internal const string FoldedStorageKey = "grocerylist_folded_sections";

    private HashSet<int> _folded = [];

    public IReadOnlyList<GroceryCategory> Categories { get; private set; } = [];

    public IReadOnlyDictionary<string, int> RememberedNames { get; private set; } = new Dictionary<string, int>();

    public GroceryListSettings Settings { get; private set; } = new();

    public async Task LoadCategories()
    {
        var categoriesTask = LoadCategoriesOnly();
        var namesTask = mediator.Send(new GetRememberedGroceryNamesAction.Query());
        var settingsTask = mediator.Send(new GetGroceryListSettingsAction.Query());
        var categories = await categoriesTask;
        var names = await namesTask;
        var settings = await settingsTask;
        if (categories is null) return;

        Categories = categories;
        if (names.Handle(CurrentSnackbar).IsSuccess)
            RememberedNames = names.Value.ToDictionary(n => n.Name, n => n.CategoryId);
        if (settings.Handle(CurrentSnackbar).IsSuccess)
            Settings = settings.Value;
        NotifyStateChanged();
    }

    // The categories, after creating the defaults for a user who has none; null when a request failed (shown).
    private async Task<List<GroceryCategory>?> LoadCategoriesOnly()
    {
        var result = await mediator.Send(new GetGroceryCategoriesAction.Query());
        if (result.Handle(CurrentSnackbar).IsFailed) return null;
        if (result.Value.Count > 0) return result.Value;

        var created = await mediator.Send(new CreateDefaultGroceryCategoriesAction.Command());
        return created.Handle(CurrentSnackbar).IsFailed ? null : created.Value;
    }

    public GroceryCategory? CategoryFor(string? name) => CategoryResolver.Resolve(name, Categories, RememberedNames);

    public async Task<bool> MoveName(string name, int categoryId)
    {
        if (CategoryFor(name)?.Id == categoryId) return true;

        var result = await mediator.Send(new RememberGroceryNameAction.Command(name, categoryId));
        if (result.Handle(CurrentSnackbar).IsFailed) return false;

        RememberedNames = new Dictionary<string, int>(RememberedNames) { [CategoryResolver.Normalise(name)] = categoryId };
        NotifyStateChanged();
        return true;
    }

    public async Task LearnFromTicks(IReadOnlyList<GroceryItem> picked, bool basketCleared)
    {
        if (Categories.Count == 0) return;

        var trips = SectionOrderLearner.CompleteTrips(picked, Settings.LearnedUntil, time.GetUtcNow().UtcDateTime, basketCleared);
        if (trips.Count == 0) return;

        var learnedUntil = trips[^1][^1].PickedAt!.Value;
        var learned = Categories;
        var orders = new List<LearnedCategoryOrder>();
        if (Settings.OrderMode == SectionOrderMode.Learned)
        {
            foreach (var trip in trips)
            {
                var observed = trip.Select(item => CategoryFor(item.Name)?.Id).OfType<int>().Distinct().ToList();
                orders = SectionOrderLearner.Learn(learned, observed);
                learned = SectionOrderLearner.Apply(learned, orders);
            }
        }

        var result = await mediator.Send(new ApplyLearnedSectionOrderAction.Command(orders, learnedUntil));
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        Categories = learned;
        Settings = Settings with { LearnedUntil = learnedUntil };
        NotifyStateChanged();
    }

    public bool IsFolded(int categoryId) => _folded.Contains(categoryId);

    public async Task ToggleFolded(int categoryId)
    {
        if (!_folded.Remove(categoryId)) _folded.Add(categoryId);
        NotifyStateChanged();
        await localStorage.SetItemAsync(FoldedStorageKey, _folded.Order().ToList());
    }

    public async Task LoadFolded()
    {
        try
        {
            _folded = [.. await localStorage.GetItemAsync<List<int>>(FoldedStorageKey) ?? []];
        }
        catch (JsonException)
        {
            _folded = [];
        }

        NotifyStateChanged();
    }

    public async Task<bool> AddCategory(string name, string? emoji)
    {
        var result = await mediator.Send(new AddGroceryCategoryAction.Command(name, emoji));
        if (result.Handle(CurrentSnackbar).IsFailed) return false;

        await LoadCategories();
        return true;
    }

    public async Task<bool> UpdateCategory(int id, string name, string? emoji)
    {
        var result = await mediator.Send(new UpdateGroceryCategoryAction.Command(id, name, emoji));
        if (result.Handle(CurrentSnackbar).IsFailed) return false;

        await LoadCategories();
        return true;
    }

    public async Task<bool> DeleteCategory(int id, int moveToId)
    {
        var result = await mediator.Send(new DeleteGroceryCategoryAction.Command(id, moveToId));
        if (result.Handle(CurrentSnackbar).IsFailed) return false;

        await LoadCategories();
        return true;
    }

    public string? ValidateName(string? name, int? categoryId, IStringLocalizer<GroceryCategoriesResource> localizer)
    {
        var normalised = CategoryResolver.Normalise(name);
        if (normalised.Length == 0) return localizer["GroceryCategory_NameRequired"];

        var taken = Categories.Any(c => c.Id != categoryId
            && CategoryResolver.Normalise(c.DisplayNameWithoutEmoji(localizer)) == normalised);
        return taken ? localizer["GroceryCategory_NameTaken"] : null;
    }
}
