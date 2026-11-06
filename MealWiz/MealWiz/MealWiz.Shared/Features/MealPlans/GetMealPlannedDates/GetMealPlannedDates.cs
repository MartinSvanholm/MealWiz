using System.Globalization;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;
using Constants = Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.MealPlans.GetMealPlannedDates;

public static class GetMealPlannedDates
{
    public record Query(DateTime From, DateTime To) : IRequest<Result<List<PlannedMealDate>>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<List<PlannedMealDate>>>
    {
        public async Task<Result<List<PlannedMealDate>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var from = request.From.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var to = request.To.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // RLS limits the rows to the signed-in user's plans.
            var result = await Result.Try(async Task<ModeledResponse<PlannedMealDateDb>> () => await supabaseClient
                .From<PlannedMealDateDb>()
                .Select("id,meal_date,fk_meal")
                .Filter("meal_date", Constants.Operator.GreaterThanOrEqual, from)
                .Filter("meal_date", Constants.Operator.LessThanOrEqual, to)
                .Order("meal_date", Constants.Ordering.Ascending)
                .Get(cancellationToken));

            if (result.IsFailed) return result.ToResult<List<PlannedMealDate>>();

            return Result.Ok(result.Value.Models.Select(db => new PlannedMealDate(db)).ToList());
        }
    }
}
