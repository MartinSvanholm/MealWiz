using System.Globalization;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;

public static class RemoveMealFromMealPlan
{
    public record Command(int MealId, int MealPlanId, DateTime MealDate) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            string mealDate = request.MealDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var result = await Result.Try(async Task() => await supabaseClient
                .From<MealPlanMealDb>()
                .Where(x => x.MealPlanId == request.MealPlanId)
                .Where(x => x.MealDate == mealDate)
                .Where(x => x.MealId == request.MealId)
                .Delete());

            if (result.IsSuccess)
            {
                result = result.WithSuccess("Meal removed");
            }

            return result;
        }
    }
}