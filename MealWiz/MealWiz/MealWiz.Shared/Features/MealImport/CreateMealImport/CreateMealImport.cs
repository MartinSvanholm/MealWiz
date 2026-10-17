using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.MealImport.CreateMealImport;

public static class CreateMealImport
{
    /// <param name="Id">Client-generated, so the caller can subscribe to the row before the Function runs.</param>
    public record Command(Guid Id, string SourceUrl) : IRequest<Result<Models.MealImport>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<Models.MealImport>>
    {
        public async Task<Result<Models.MealImport>> Handle(Command request, CancellationToken cancellationToken)
        {
            var mealImportDb = new Models.MealImport
            {
                Id = request.Id,
                SourceUrl = request.SourceUrl,
                Status = MealImportStatus.Pending
            }.MapToMealImportDb();

            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

            mealImportDb.CreatedAt = DateTime.UtcNow;
            mealImportDb.CreatedBy = new Guid(userId);

            var insertResult = await Result.Try(async Task<ModeledResponse<MealImportDb>> () => await supabaseClient
                .From<MealImportDb>()
                .Insert(mealImportDb, cancellationToken: cancellationToken));

            return insertResult.Map(r => new Models.MealImport(r.Model ?? mealImportDb));
        }
    }
}
