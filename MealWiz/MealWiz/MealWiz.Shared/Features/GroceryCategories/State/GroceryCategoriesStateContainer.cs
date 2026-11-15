using System.Text.Json;
using Blazored.LocalStorage;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using MudBlazor;
using CreateDefaultGroceryCategoriesAction = MealWiz.Shared.Features.GroceryCategories.CreateDefaultGroceryCategories.CreateDefaultGroceryCategories;
using GetGroceryCategoriesAction = MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories.GetGroceryCategories;

namespace MealWiz.Shared.Features.GroceryCategories.State;

public interface IGroceryCategoriesStateContainer
{
    ISnackbar CurrentSnackbar { get; set; }

    /// <summary>The user's categories by position; empty until <see cref="LoadCategories"/> succeeds.</summary>
    IReadOnlyList<GroceryCategory> Categories { get; }

    event Action OnStateChanged;

    /// <summary>Loads the user's categories, creating the eleven defaults the first time (when the user has none).</summary>
    Task LoadCategories();

    /// <summary>The user's category for a grocery name (<see cref="CategoryResolver.Resolve"/>); null before the categories load.</summary>
    GroceryCategory? CategoryFor(string? name);

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

    public async Task LoadCategories()
    {
        var result = await mediator.Send(new GetGroceryCategoriesAction.Query());
        if (result.Handle(CurrentSnackbar).IsFailed) return;

        var categories = result.Value;
        if (categories.Count == 0)
        {
            var created = await mediator.Send(new CreateDefaultGroceryCategoriesAction.Command());
            if (created.Handle(CurrentSnackbar).IsFailed) return;
            categories = created.Value;
        }

        Categories = categories;
        NotifyStateChanged();
    }

    public GroceryCategory? CategoryFor(string? name) => CategoryResolver.Resolve(name, Categories);

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
