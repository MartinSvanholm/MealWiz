using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.MealPlans.AddMealToMealPlan;

public static class AddMealToMealPlan
{
    public record Command(Meal Meal) : IRequest<Result<Meal>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<Meal>>
    {
        public async Task<Result<Meal>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Meal.MealPlan == null || request.Meal.MealDate == null)
            {
                return new Result().WithError("Meal plan or meal date cannot be empty");
            }

            var mealPlanMealDb = new MealPlanMealDb()
            {
                Id = 0,
                MealDate = request.Meal.MealDate.Value.ToString("yyyy-MM-dd"),
                MealId = request.Meal.Id,
                MealPlanId = request.Meal.MealPlan.Id,
                CreatedAt = DateTime.Now,
                CreatedBy = new Guid(supabaseClient.Auth.CurrentSession?.User.Id)
            };

            var result = await Result.Try(async Task<ModeledResponse<MealPlanMealDb>> () => await supabaseClient
                .From<MealPlanMealDb>()
                .Insert(mealPlanMealDb));

            return new Result<Meal>().WithValue(request.Meal).WithSuccess("Meal added to plan");
        }
    }
}