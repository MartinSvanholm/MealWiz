using FluentResults;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Helpers;
using MediatR;
using Supabase.Postgrest;
using Supabase.Postgrest.Responses;
using Client = Supabase.Client;

namespace TheMealWorkshop.Shared.Features.GroceryCategories.SetSectionOrderMode;

public static class SetSectionOrderMode
{
    /// <summary>Learned ("Follow my shopping again") or manual. Positions stay as they are.</summary>
    public record Command(SectionOrderMode Mode) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

            var row = new GroceryListOrderModeDb
            {
                OrderMode = request.Mode.ToDbValue(),
                CreatedBy = new Guid(userId),
                UpdatedAt = DateTime.UtcNow,
            };

            var result = await Result.Try(async Task<ModeledResponse<GroceryListOrderModeDb>> () => await supabaseClient
                .From<GroceryListOrderModeDb>()
                .Upsert(row, new QueryOptions
                {
                    OnConflict = "created_by",
                    DuplicateResolution = QueryOptions.DuplicateResolutionType.MergeDuplicates,
                    Returning = QueryOptions.ReturnType.Minimal,
                }, cancellationToken));

            return result.ToResult();
        }
    }
}
