using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;

namespace MealWiz.Shared.Features.GroceryCategories.ReorderGroceryCategories;

public static class ReorderGroceryCategories
{
    /// <summary>
    /// The user's own section order: every category id once, in order (Other last). Learning stops (manual mode), in
    /// one call to <c>reorder_grocery_categories</c>.
    /// </summary>
    public record Command(List<int> CategoryIds) : IRequest<Result>;

    public class Handler(Client supabaseClient, IStringLocalizer<GroceryCategoriesResource> localizer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken) =>
            await Result.Try(
                async Task () => await supabaseClient.Rpc("reorder_grocery_categories", new Dictionary<string, object>
                {
                    ["category_ids"] = request.CategoryIds,
                }),
                exception => GroceryCategoryErrors.Map(exception, localizer));
    }
}
