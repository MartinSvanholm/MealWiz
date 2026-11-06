using System.Globalization;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.MealPlans.AddMealToMealPlan;

public static class AddMealToMealPlan
{
    public record Command(Meal Meal) : IRequest<Result<int>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<int>>
    {
        public async Task<Result<int>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Meal.MealPlan == null || request.Meal.MealDate == null)
                return Result.Fail<int>("Meal plan or meal date cannot be empty");

            if (supabaseClient.Auth.CurrentSession?.User == null)
                return Result.Fail<int>("Not authenticated");

            var mealPlanMealDb = new MealPlanMealDb()
            {
                Id = 0,
                MealDate = request.Meal.MealDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                MealId = request.Meal.Id,
                MealPlanId = request.Meal.MealPlan.Id,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = new Guid(supabaseClient.Auth.CurrentSession.User.Id)
            };

            var result = await Result.Try(async Task<ModeledResponse<MealPlanMealDb>> () => await supabaseClient
                .From<MealPlanMealDb>()
                .Insert(mealPlanMealDb));

            if (result.IsFailed) return result.ToResult<int>();

            return result.Value.Model is { } model
                ? Result.Ok(model.Id).WithSuccess("Meal added to plan")
                : Result.Fail<int>("The meal could not be added to the plan.");
        }
    }
}