using FluentResults;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
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
                var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
                if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

                mealDb.CreatedAt = DateTime.UtcNow;
                mealDb.CreatedBy = new Guid(userId);

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