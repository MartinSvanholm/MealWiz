using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;

namespace MealWiz.Shared.Features.MealImport.UploadMealImportPictures;

public static class UploadMealImportPictures
{
    /// <param name="JobId">The picture job's id; it names the folder the pictures go in.</param>
    /// <param name="Pictures">One to <see cref="MealImportPictures.MaxPictures"/> JPEGs, in the order the user added them.</param>
    public record Command(Guid JobId, IReadOnlyList<byte[]> Pictures) : IRequest<Result<IReadOnlyList<string>>>;

    /// <summary>
    /// Uploads the pictures one at a time to <c>{userId}/{jobId}/{index}.jpg</c> in the private pictures bucket, as the
    /// signed-in user (the bucket's policies only let a user write inside their own folder). Returns the paths in order.
    /// If one upload fails, the ones already uploaded are removed again.
    /// </summary>
    public class Handler(Client supabaseClient, IStringLocalizer<MealImportResource> localizer)
        : IRequestHandler<Command, Result<IReadOnlyList<string>>>
    {
        public async Task<Result<IReadOnlyList<string>>> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

            if (request.Pictures.Count is < 1 or > MealImportPictures.MaxPictures)
                return Result.Fail(localizer["Import_PictureUploadFailed"]);

            var uploaded = new List<string>();
            for (var index = 0; index < request.Pictures.Count; index++)
            {
                var path = MealImportPictures.PathFor(new Guid(userId), request.JobId, index);
                var picture = request.Pictures[index];

                // A fresh From() per call: it snapshots the auth headers.
                var uploadResult = await Result.Try(async Task<string> () => await supabaseClient.Storage
                    .From(MealImportPictures.BucketId)
                    .Upload(picture, path,
                        new Supabase.Storage.FileOptions { ContentType = "image/jpeg", Upsert = false },
                        onProgress: null, inferContentType: false));

                if (uploadResult.IsFailed)
                {
                    await Result.Try(async Task<object?> () =>
                    {
                        if (uploaded.Count > 0)
                            await supabaseClient.Storage.From(MealImportPictures.BucketId).Remove(uploaded);
                        return null;
                    });
                    return Result.Fail(localizer["Import_PictureUploadFailed"]);
                }

                uploaded.Add(path);
            }

            return Result.Ok<IReadOnlyList<string>>(uploaded);
        }
    }
}
