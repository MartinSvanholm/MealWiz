using System.Text.Json;
using Blazored.LocalStorage;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;
using CreateDefaultGroceryCategoriesAction = MealWiz.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories.CreateDefaultGroceryCategories;
using GetGroceryCategoriesAction = MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories.GetGroceryCategories;
using GetRememberedGroceryNamesAction = MealWiz.Shared.Features.GroceryCategories.GetRememberedGroceryNames.GetRememberedGroceryNames;
using RememberGroceryNameAction = MealWiz.Shared.Features.GroceryCategories.RememberGroceryName.RememberGroceryName;

namespace MealWiz.Shared.Features.GroceryCategories.State;

public interface IGroceryCategoriesStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }

    /// <summary>The user's categories by position; empty until <see cref="LoadCategories"/> succeeds.</summary>
    IReadOnlyList<GroceryCategory> Categories { get; }

    event Action OnStateChanged;

    /// <summary>Normalised grocery name → the category the user put it in; loaded with the categories.</summary>
    IReadOnlyDictionary<string, int> RememberedNames { get; }

    /// <summary>
    /// Loads the user's categories, creating the eleven defaults the first time (when the user has none), and the
    /// remembered names. A failed remembered-names load shows its error and keeps the categories.
    /// </summary>
    Task LoadCategories();

    /// <summary>The user's category for a grocery name (<see cref="CategoryResolver.Resolve"/>); null before the categories load.</summary>
    GroceryCategory? CategoryFor(string? name);

    /// <summary>
    /// Puts a name in a category everywhere (remembered). True when the name now resolves there, including when it
    /// already did (nothing is sent). On failure the error shows and nothing changes.
    /// </summary>
    Task<bool> MoveName(string name, int categoryId);

    /// <summary>Whether a section is folded on the grocery list (heading and count only). Kept on this device.</summary>
    bool IsFolded(int categoryId);

    /// <summary>Folds or unfolds a section and saves the folded sections to local storage.</summary>
    Task ToggleFolded(int categoryId);

    /// <summary>Reads the folded sections from local storage; an unreadable value folds nothing.</summary>
    Task LoadFolded();
}

public class GroceryCategoriesStateContainer(IMediator mediator, ILocalStorageService localStorage)
    : StateContainerBase, IGroceryCategoriesStateContainer
{
    internal const string FoldedStorageKey = "grocerylist_folded_sections";

    private HashSet<int> _folded = [];

    public IReadOnlyList<GroceryCategory> Categories { get; private set; } = [];

    public IReadOnlyDictionary<string, int> RememberedNames { get; private set; } = new Dictionary<string, int>();

    public async Task LoadCategories()
    {
        var categoriesTask = LoadCategoriesOnly();
        var namesTask = mediator.Send(new GetRememberedGroceryNamesAction.Query());
        var categories = await categoriesTask;
        var names = await namesTask;
        if (categories is null) return;

        Categories = categories;
        if (names.Handle(CurrentSnackbar).IsSuccess)
            RememberedNames = names.Value.ToDictionary(n => n.Name, n => n.CategoryId);
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
}
