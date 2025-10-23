using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MediatR;
using Supabase.Postgrest;
using Supabase.Postgrest.Responses;
using Client = Supabase.Client;

namespace MealWiz.Shared.Features.MealPlans.GetMealPlanByDate;

public static class GetMealPlanByDate
{
    public record Query(DateTime Date) : IRequest<Result<MealPlan?>>;

    public class Handler(Client _supabaseClient) : IRequestHandler<Query, Result<MealPlan?>>
    {
        public async Task<Result<MealPlan?>> Handle(Query request, CancellationToken cancellationToken)
        {
            string date = request.Date.Date.ToString("yyyy-MM-dd");

            var result = await Result.Try(async Task<ModeledResponse<MealPlanDb>> () => await _supabaseClient.From<MealPlanDb>()
                .Select("*")
                .Filter("start_date", Constants.Operator.LessThanOrEqual, date)
                .Filter("end_date", Constants.Operator.GreaterThanOrEqual, date)
                .Get());

            if (result.IsFailed) return Result.Fail(result.Errors);

            if (result.Value.Model == null) return Result.Ok();

            var mealPlan = new MealPlan(result.Value.Model);
            return Result.Ok(mealPlan);
        }
    }
}