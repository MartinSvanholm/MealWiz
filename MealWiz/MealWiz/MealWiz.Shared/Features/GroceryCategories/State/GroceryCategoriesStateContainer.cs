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
}

public class GroceryCategoriesStateContainer(IMediator mediator) : StateContainerBase, IGroceryCategoriesStateContainer
{
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
}
