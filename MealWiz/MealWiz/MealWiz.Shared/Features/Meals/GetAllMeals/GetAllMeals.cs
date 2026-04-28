using FluentResults;
using MealWiz.Shared.Features.Meals.Models;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.Meals.GetAllMeals;

public static class GetAllMeals
{
    public record Query() : IRequest<Result<List<Meal>>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Query, Result<List<Meal>>>
    {
        public async Task<Result<List<Meal>>> Handle(Query request, CancellationToken cancellationToken)
        {
            return await Result.Try(async Task<List<Meal>>() =>
            {
                var result = await supabaseClient.From<MealDb>().Get();
                return result.Models.Select(db => new Meal(db)).ToList();
            });
        }
    }
}