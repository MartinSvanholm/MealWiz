using FluentResults;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;

namespace TheMealWorkshop.Shared.Features.GroceryCategories.DeleteGroceryCategory;

public static class DeleteGroceryCategory
{
    /// <summary>
    /// Deletes a category and hands its items to <paramref name="MoveToId"/>: its remembered names move there, and so
    /// do the dictionary words of its default key (one call to <c>delete_grocery_category</c>). Other can't be deleted.
    /// </summary>
    public record Command(int Id, int MoveToId) : IRequest<Result>;

    public class Handler(Client supabaseClient, IStringLocalizer<GroceryCategoriesResource> localizer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken) =>
            await Result.Try(
                async Task () => await supabaseClient.Rpc("delete_grocery_category", new Dictionary<string, object>
                {
                    ["category_id"] = request.Id,
                    ["move_to_id"] = request.MoveToId,
                }),
                exception => GroceryCategoryErrors.Map(exception, localizer));
    }
}
