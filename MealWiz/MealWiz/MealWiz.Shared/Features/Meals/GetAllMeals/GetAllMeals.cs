using FluentResults;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.Meals.GetAllMeals;

public static class GetAllMeals
{
    public record Query() : IRequest<Result<List<Meal>>>;

    public class Handler(Client _supabaseClient) : IRequestHandler<Query, Result<List<Meal>>>
    {
        public async Task<Result<List<Meal>>> Handle(Query request, CancellationToken cancellationToken)
        {
            var result = await _supabaseClient.From<MealDb>().Select("*, ingredients(*)").Get();
            var meals = result.Models.Select(db => new Meal(db)).ToList();

            return Result.Ok(meals);
        }
    }
}