using FluentResults;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.Meals.EditMeal;

public static class EditMeal
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

                var supabaseResult = await Result.Try(async Task () => 
                {
                    await supabaseClient.From<MealDb>().Insert(mealDb);

                    Result.Ok();
                });

                return supabaseResult;
            }
            else
            {
                mealDb.UpdatedAt = DateTime.UtcNow;

                var supabaseResult = await Result.Try(async Task () =>
                {
                    await supabaseClient.From<MealDb>().Update(mealDb);

                    Result.Ok();
                });

                return supabaseResult;
            }
        }
    }
}