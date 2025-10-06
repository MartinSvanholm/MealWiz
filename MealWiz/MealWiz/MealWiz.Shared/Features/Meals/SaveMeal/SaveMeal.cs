using FluentResults;
using MealWiz.Shared.Features.Meals.Models;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.Meals.SaveMeal;

public static class SaveMeal
{
    public record Command(Meal Meal) : IRequest<Result>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var mealDb = request.Meal.MapToMealDb();

            if (mealDb.Id == 0)
            {
                mealDb.CreatedAt = DateTime.UtcNow;
                mealDb.CreatedBy = new Guid(supabaseClient.Auth.CurrentSession?.User.Id);

                return await Result.Try(async Task () => await supabaseClient
                    .From<MealDb>()
                    .Insert(mealDb));
            }
            else
            {
                mealDb.UpdatedAt = DateTime.UtcNow;

                return await Result.Try(async Task () => await supabaseClient
                    .From<MealDb>()
                    .Update(mealDb));
            }
        }
    }
}