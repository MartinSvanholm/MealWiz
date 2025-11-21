using FluentResults;
using MealWiz.Shared.Features.Meals.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.Meals.SaveMeal;

public static class SaveMeal
{
    public record Command(Meal Meal) : IRequest<Result<Meal>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<Meal>>
    {
        public async Task<Result<Meal>> Handle(Command request, CancellationToken cancellationToken)
        {
            var mealDb = request.Meal.MapToMealDb();

            if (mealDb.Id == 0)
            {
                mealDb.CreatedAt = DateTime.UtcNow;
                mealDb.CreatedBy = new Guid(supabaseClient.Auth.CurrentSession?.User.Id);

                var insertResult = await Result.Try(async Task<ModeledResponse<MealDb>> () => await supabaseClient
                    .From<MealDb>()
                    .Insert(mealDb));

                return insertResult.Map(r => new Meal(r.Model ?? new()));
            }
            else
            {
                mealDb.UpdatedAt = DateTime.UtcNow;

                var updateResult = await Result.Try(async Task<ModeledResponse<MealDb>> () => await supabaseClient
                    .From<MealDb>()
                    .Update(mealDb));

                return updateResult.Map(r => new Meal(r.Model ?? new()));
            }
        }
    }
}