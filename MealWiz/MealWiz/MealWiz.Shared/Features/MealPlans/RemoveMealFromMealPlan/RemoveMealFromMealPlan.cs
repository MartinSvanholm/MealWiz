using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.MealPlans.RemoveMealFromMealPlan;

public static class RemoveMealFromMealPlan
{
    public record Command(int MealId) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task() => await supabaseClient
                .From<MealPlanMealDb>()
                .Where(x => x.MealId == request.MealId)
                .Delete());

            if (result.IsSuccess)
            {
                result.WithSuccess("Meal removed");
            }

            return result;
        }
    }
}