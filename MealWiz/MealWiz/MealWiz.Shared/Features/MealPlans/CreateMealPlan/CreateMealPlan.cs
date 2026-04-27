using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace MealWiz.Shared.Features.MealPlans.CreateMealPlan;

public static class CreateMealPlan
{
    public record Command(MealPlan MealPlan) : IRequest<Result<MealPlan>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<MealPlan>>
    {
        public async Task<Result<MealPlan>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (supabaseClient.Auth.CurrentSession?.User == null)
                return Result.Fail<MealPlan>("Not authenticated");

            request.MealPlan.CreatedAt = DateTime.UtcNow;
            request.MealPlan.CreatedBy = new Guid(supabaseClient.Auth.CurrentSession.User.Id);

            var result = await Result.Try(async Task<ModeledResponse<MealPlanDb>>() => await supabaseClient
                .From<MealPlanDb>()
                .Insert(request.MealPlan.MapToMealPlanDb()));

            return result.Map(x => new MealPlan(x.Model));
        }
    }
}