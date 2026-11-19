using System.Globalization;
using FluentResults;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;
using Supabase.Postgrest.Exceptions;
using Supabase.Postgrest.Responses;

namespace TheMealWorkshop.Shared.Features.GroceryCategories.AddGroceryCategory;

public static class AddGroceryCategory
{
    /// <summary>Adds a category just above Other (one call to <c>add_grocery_category</c>) and returns it.</summary>
    public record Command(string Name, string? Emoji) : IRequest<Result<GroceryCategory>>;

    public class Handler(Client supabaseClient, IStringLocalizer<GroceryCategoriesResource> localizer)
        : IRequestHandler<Command, Result<GroceryCategory>>
    {
        public async Task<Result<GroceryCategory>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (supabaseClient.Auth.CurrentSession?.User?.Id is null) return Result.Fail(ErrorMessages.NotSignedIn);

            var added = await Result.Try(
                async Task<string?> () => (await supabaseClient.Rpc("add_grocery_category", new Dictionary<string, object?>
                {
                    ["category_name"] = request.Name.Trim(),
                    ["category_emoji"] = request.Emoji?.Trim() ?? string.Empty,
                })).Content,
                exception => GroceryCategoryErrors.Map(exception, localizer));
            if (added.IsFailed) return added.ToResult<GroceryCategory>();
            if (!long.TryParse(added.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                return Result.Fail(localizer["GroceryCategory_NotAdded"]);

            var row = await Result.Try(async Task<ModeledResponse<GroceryCategoryDb>> () => await supabaseClient
                .From<GroceryCategoryDb>()
                .Where(x => x.Id == (int)id)
                .Get(cancellationToken));
            if (row.IsFailed) return row.ToResult<GroceryCategory>();

            return row.Value.Model is { } model
                ? Result.Ok(new GroceryCategory(model))
                : Result.Fail(localizer["GroceryCategory_NotAdded"]);
        }
    }
}
