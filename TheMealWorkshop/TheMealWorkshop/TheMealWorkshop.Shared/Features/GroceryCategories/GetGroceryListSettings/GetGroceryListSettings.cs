using FluentResults;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryListSettings;

public static class GetGroceryListSettings
{
    /// <summary>The user's settings; the defaults (learned, nothing learned yet) when the user has no row.</summary>
    public record Query() : IRequest<Result<GroceryListSettings>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<GroceryListSettings>>
    {
        public async Task<Result<GroceryListSettings>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<ModeledResponse<GroceryListSettingsDb>> () => await supabaseClient
                .From<GroceryListSettingsDb>()
                .Get(cancellationToken));

            return result.Map(value => value.Model is { } row ? new GroceryListSettings(row) : new GroceryListSettings());
        }
    }
}
