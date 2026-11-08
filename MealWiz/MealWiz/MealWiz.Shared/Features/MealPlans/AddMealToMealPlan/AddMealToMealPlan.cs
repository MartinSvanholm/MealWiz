using System.Globalization;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.MealPlans.AddMealToMealPlan;

public static class AddMealToMealPlan
{
    public record Command(Meal Meal) : IRequest<Result<int>>;

    public class Handler(Client supabaseClient, IStringLocalizer<MealPlansResource> localizer) : IRequestHandler<Command, Result<int>>
    {
        public async Task<Result<int>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (request.Meal.MealPlan == null || request.Meal.MealDate == null)
                return Result.Fail<int>(localizer["MealPlan_PlanOrDateMissing"]);

            if (supabaseClient.Auth.CurrentSession?.User == null)
                return Result.Fail<int>(ErrorMessages.NotSignedIn);

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
                ? Result.Ok(model.Id).WithSuccess(localizer["MealPlan_MealAdded"])
                : Result.Fail<int>(localizer["MealPlan_MealNotAdded"]);
        }
    }
}