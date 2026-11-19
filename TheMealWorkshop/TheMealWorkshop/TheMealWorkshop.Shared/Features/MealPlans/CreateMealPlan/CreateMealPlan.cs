using FluentResults;
using TheMealWorkshop.Shared.Features.MealPlans.Models;
using TheMealWorkshop.Shared.Helpers;
using MediatR;
using Supabase;
using Supabase.Postgrest.Responses;

namespace TheMealWorkshop.Shared.Features.MealPlans.CreateMealPlan;

public static class CreateMealPlan
{
    public record Command(MealPlan MealPlan) : IRequest<Result<MealPlan>>;

    public class Handler(Client supabaseClient) : IRequestHandler<Command, Result<MealPlan>>
    {
        public async Task<Result<MealPlan>> Handle(Command request, CancellationToken cancellationToken)
        {
            if (supabaseClient.Auth.CurrentSession?.User == null)
                return Result.Fail<MealPlan>(ErrorMessages.NotSignedIn);

            request.MealPlan.CreatedAt = DateTime.UtcNow;
            request.MealPlan.CreatedBy = new Guid(supabaseClient.Auth.CurrentSession.User.Id);

            var result = await Result.Try(async Task<ModeledResponse<MealPlanDb>>() => await supabaseClient
                .From<MealPlanDb>()
                .Insert(request.MealPlan.MapToMealPlanDb()));

            return result.Map(x => new MealPlan(x.Model));
        }
    }
}