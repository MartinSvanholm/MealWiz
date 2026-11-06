using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;

namespace MealWiz.Shared.Features.MealImport.GetMealImport;

public static class GetMealImport
{
    public record Query(Guid Id) : IRequest<Result<Models.MealImport>>;

    public class Handler(Client supabaseClient, IStringLocalizer<MealImportResource> localizer) : IRequestHandler<Query, Result<Models.MealImport>>
    {
        public async Task<Result<Models.MealImport>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task<MealImportDb?> () => await supabaseClient
                .From<MealImportDb>()
                .Where(r => r.Id == request.Id)
                .Single(cancellationToken));

            if (result.IsFailed) return result.ToResult<Models.MealImport>();
            if (result.Value is null) return Result.Fail(localizer["Import_NotFound"]);

            return new Models.MealImport(result.Value);
        }
    }
}
