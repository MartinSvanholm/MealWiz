using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.MealImport.CreatePictureMealImport;

public static class CreatePictureMealImport
{
    /// <param name="Id">Client-generated, so the caller can subscribe to the row before the Function runs.</param>
    /// <param name="PicturePaths">The uploaded pictures, in the order the user added them.</param>
    /// <param name="Language">The app language the meal is written in ("en" or "da").</param>
    /// <param name="Hint">The user's hint for a retry; blank becomes null, longer than <see cref="MealImportPictures.MaxHintLength"/> fails.</param>
    public record Command(Guid Id, IReadOnlyList<string> PicturePaths, string Language, string? Hint)
        : IRequest<Result<Models.MealImport>>;

    /// <summary>Inserts the pending picture job (no <c>source_url</c>); the AnalyzeMealPicture Function resolves it.</summary>
    public class Handler(Client supabaseClient, IStringLocalizer<MealImportResource> localizer)
        : IRequestHandler<Command, Result<Models.MealImport>>
    {
        public async Task<Result<Models.MealImport>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

            var hint = string.IsNullOrWhiteSpace(request.Hint) ? null : request.Hint.Trim();
            if (hint is { Length: > MealImportPictures.MaxHintLength })
                return Result.Fail(localizer["Import_HintTooLong", MealImportPictures.MaxHintLength]);

            var mealImportDb = new Models.MealImport
            {
                Id = request.Id,
                PicturePaths = request.PicturePaths,
                Language = request.Language,
                Hint = hint,
                Status = MealImportStatus.Pending
            }.MapToMealImportDb();

            mealImportDb.CreatedAt = DateTime.UtcNow;
            mealImportDb.CreatedBy = new Guid(userId);

            var insertResult = await Result.Try(async Task<ModeledResponse<MealImportDb>> () => await supabaseClient
                .From<MealImportDb>()
                .Insert(mealImportDb, cancellationToken: cancellationToken));

            return insertResult.Map(r => new Models.MealImport(r.Model ?? mealImportDb));
        }
    }
}
