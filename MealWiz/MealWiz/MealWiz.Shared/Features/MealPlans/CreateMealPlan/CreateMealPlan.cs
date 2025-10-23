using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MediatR;
using Supabase;

namespace MealWiz.Shared.Features.MealPlans.CreateMealPlan;

public static class CreateMealPlan
{
    public record Command(MealPlan MealPlan) : IRequest<Result<MealPlan>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<MealPlan>>
    {
        public async Task<Result<MealPlan>> Handle(Command request, CancellationToken cancellationToken)
        {
            request.MealPlan.CreatedAt = DateTime.Now;
            request.MealPlan.CreatedBy = new Guid(supabaseClient.Auth.CurrentSession.User.Id);

            var result = await Result.Try(() => supabaseClient
                .From<MealPlanDb>()
                .Insert(request.MealPlan.MapToMealPlanDb()));

            return result.Map(x => new MealPlan(x.Model));
        }
    }
}