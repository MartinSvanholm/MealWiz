using System.Globalization;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.MealPlans.SwapMealInMealPlan;

public static class SwapMealInMealPlan
{
    public record Command(int MealPlanId, DateTime MealDate, int CurrentMealId, int NewMealId) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            string mealDate = request.MealDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            // One update of the day's row, so a failure never leaves the day empty.
            var result = await Result.Try(async Task<ModeledResponse<MealPlanMealDb>> () => await supabaseClient
                .From<MealPlanMealDb>()
                .Where(x => x.MealPlanId == request.MealPlanId)
                .Where(x => x.MealDate == mealDate)
                .Where(x => x.MealId == request.CurrentMealId)
                .Set(x => x.MealId, request.NewMealId)
                .Set(x => x.UpdatedAt!, DateTime.UtcNow)
                .Update(cancellationToken: cancellationToken));

            if (result.IsFailed) return result.ToResult();

            // RLS hides other users' rows, so they also end up here.
            if (result.Value.Models.Count == 0) return Result.Fail("Meal not found in plan");

            return Result.Ok().WithSuccess("Meal swapped");
        }
    }
}
