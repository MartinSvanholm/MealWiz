using FluentResults;
using MealWiz.Shared.Features.GroceryList.Models;
using MediatR;
using Supabase;
using static Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.GroceryList.DeleteGroceryItemSources;

/// <summary>
/// Deletes the given grocery item sources in one request, keeping their items (MEA-93): a deleted meal's or
/// ingredient's links on items another planned meal or the user still needs. Ids RLS hides are skipped.
/// </summary>
public static class DeleteGroceryItemSources
{
    public record Command(List<int> SourceIds) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.SourceIds.Count == 0) return Result.Ok();

            return await Result.Try(async Task () => await supabaseClient
                .From<GroceryItemSourceDb>()
                .Filter(x => x.Id, Operator.In, request.SourceIds)
                .Delete(cancellationToken: cancellationToken));
        }
    }
}
