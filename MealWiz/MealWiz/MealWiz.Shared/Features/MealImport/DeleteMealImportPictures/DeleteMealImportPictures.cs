using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.MealImport.DeleteMealImportPictures;

public static class DeleteMealImportPictures
{
    /// <param name="Paths">Object paths in the pictures bucket. An empty list is a success.</param>
    public record Command(IReadOnlyList<string> Paths) : IRequest<Result>;

    /// <summary>Removes the objects through the Storage API (never SQL), as the signed-in user (owner-only policy).</summary>
    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Paths.Count == 0) return Result.Ok();

            var result = await Result.Try(async Task<object?> () =>
            {
                await supabaseClient.Storage.From(MealImportPictures.BucketId).Remove(request.Paths.ToList());
                return null;
            });

            return result.ToResult();
        }
    }
}
