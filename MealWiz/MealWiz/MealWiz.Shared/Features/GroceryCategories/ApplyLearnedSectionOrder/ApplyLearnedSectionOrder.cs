using System.Globalization;
using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;

namespace MealWiz.Shared.Features.GroceryCategories.ApplyLearnedSectionOrder;

public static class ApplyLearnedSectionOrder
{
    /// <summary>
    /// Writes what shopping trips taught (each category's position and score) and the last tick learned from, in one
    /// call to <c>apply_learned_section_order</c>. With no categories it only moves <paramref name="LearnedUntil"/>.
    /// </summary>
    public record Command(List<LearnedCategoryOrder> Categories, DateTime LearnedUntil) : IRequest<Result>;

    public class Handler(Client supabaseClient, IStringLocalizer<GroceryCategoriesResource> localizer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken) =>
            await Result.Try(
                async Task () => await supabaseClient.Rpc("apply_learned_section_order", new Dictionary<string, object>
                {
                    ["category_scores"] = request.Categories.ConvertAll(c => new { id = c.CategoryId, position = c.Position, score = c.Score }),
                    ["learned_until"] = request.LearnedUntil.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                }),
                exception => GroceryCategoryErrors.Map(exception, localizer));
    }
}
