using FluentResults;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.Meals.DeleteMeal;

public static class DeleteMeal
{
    public record Command(int MealId) : IRequest<Result>;
    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var result = await Result.Try(async Task () =>
            {
                 await supabaseClient.From<MealDb>()
                    .Where(m => m.Id == request.MealId)
                    .Delete();

                Result.Ok();
            });

            return result;
        }
    }
}