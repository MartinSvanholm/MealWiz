using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.GroceryCategories.UpdateGroceryCategory;

public static class UpdateGroceryCategory
{
    /// <summary>Renames a category and sets its emoji (empty = none). A name another category has fails.</summary>
    public record Command(int Id, string Name, string? Emoji) : IRequest<Result<GroceryCategory>>;

    public class Handler(Client supabaseClient, IStringLocalizer<GroceryCategoriesResource> localizer)
        : IRequestHandler<Command, Result<GroceryCategory>>
    {
        public async Task<Result<GroceryCategory>> Handle(Command request, CancellationToken cancellationToken)
        {
            var emoji = string.IsNullOrWhiteSpace(request.Emoji) ? null : request.Emoji.Trim();

            var result = await Result.Try(
                async Task<ModeledResponse<GroceryCategoryDb>> () => await supabaseClient
                    .From<GroceryCategoryDb>()
                    .Where(x => x.Id == request.Id)
                    .Set(x => x.Name!, request.Name.Trim())
                    .Set(x => x.Emoji!, emoji!)
                    .Set(x => x.UpdatedAt!, DateTime.UtcNow)
                    .Update(cancellationToken: cancellationToken),
                exception => GroceryCategoryErrors.Map(exception, localizer));
            if (result.IsFailed) return result.ToResult<GroceryCategory>();

            // RLS hides other users' rows, and a category deleted on another device is gone: both end up here.
            return result.Value.Model is { } model
                ? Result.Ok(new GroceryCategory(model))
                : Result.Fail(localizer["GroceryCategory_NotFound"]);
        }
    }
}
