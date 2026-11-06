using System.Globalization;
using FluentResults;
using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Helpers;
using MediatR;
using Microsoft.Extensions.Localization;
using Supabase;
using Supabase.Postgrest.Responses;
using Constants = Supabase.Postgrest.Constants;

namespace MealWiz.Shared.Features.MealPlans.SaveGeneratedWeek;

public static class SaveGeneratedWeek
{
    public record PlannedDay(DateTime Date, int MealId);

    public record Command(DateTime WeekStart, IReadOnlyList<PlannedDay> Days) : IRequest<Result>;

    public class Handler(Client supabaseClient, IStringLocalizer<MealPlansResource> localizer) : IRequestHandler<Command, Result>
    {
        public async Task<Result> Handle(Command request, CancellationToken cancellationToken)
        {
            var userId = supabaseClient.Auth.CurrentSession?.User?.Id;
            if (userId is null) return Result.Fail(ErrorMessages.NotSignedIn);

            var monday = request.WeekStart.StartOfWeek().Date;
            var sunday = monday.AddDays(6);

            if (request.Days.Count == 0) return Result.Fail(localizer["PlanWeek_NothingToSave"]);
            if (request.Days.Any(d => d.Date.Date < monday || d.Date.Date > sunday))
                return Result.Fail(localizer["PlanWeek_DayOutsideWeek"]);
            if (request.Days.Select(d => d.Date.Date).Distinct().Count() != request.Days.Count)
                return Result.Fail(localizer["PlanWeek_DayTwice"]);

            var mondayIso = Iso(monday);

            // The plan whose week contains the Monday, with the days it already has.
            var planResult = await Result.Try(async Task<ModeledResponse<MealPlanDb>> () => await supabaseClient
                .From<MealPlanDb>()
                .Select("*")
                .Filter("start_date", Constants.Operator.LessThanOrEqual, mondayIso)
                .Filter("end_date", Constants.Operator.GreaterThanOrEqual, mondayIso)
                .Get(cancellationToken));
            if (planResult.IsFailed) return planResult.ToResult();

            var plan = planResult.Value.Model;
            if (plan is null)
            {
                var newPlan = new MealPlan(monday).MapToMealPlanDb();
                newPlan.CreatedAt = DateTime.UtcNow;
                newPlan.CreatedBy = new Guid(userId);

                var createResult = await Result.Try(async Task<ModeledResponse<MealPlanDb>> () => await supabaseClient
                    .From<MealPlanDb>()
                    .Insert(newPlan));
                if (createResult.IsFailed) return createResult.ToResult();
                if (createResult.Value.Model is null) return Result.Fail(localizer["PlanWeek_NotSaved"]);

                plan = createResult.Value.Model;
                plan.MealPlanMeals = [];
            }

            var existingDates = (plan.MealPlanMeals ?? []).Select(m => m.MealDate).ToHashSet();
            var days = request.Days.OrderBy(d => d.Date).ToList();

            // A day that got a meal in the meantime is overwritten in place, so its row keeps its id.
            foreach (var day in days.Where(d => existingDates.Contains(Iso(d.Date))))
            {
                var date = Iso(day.Date);
                var updateResult = await Result.Try(async Task<ModeledResponse<MealPlanMealDb>> () => await supabaseClient
                    .From<MealPlanMealDb>()
                    .Where(x => x.MealPlanId == plan.Id)
                    .Where(x => x.MealDate == date)
                    .Set(x => x.MealId, day.MealId)
                    .Set(x => x.UpdatedAt!, DateTime.UtcNow)
                    .Update(cancellationToken: cancellationToken));
                if (updateResult.IsFailed) return updateResult.ToResult();
            }

            var newRows = days
                .Where(d => !existingDates.Contains(Iso(d.Date)))
                .Select(d => new MealPlanMealDb
                {
                    MealDate = Iso(d.Date),
                    MealId = d.MealId,
                    MealPlanId = plan.Id,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = new Guid(userId)
                })
                .ToList();

            if (newRows.Count > 0)
            {
                var insertResult = await Result.Try(async Task<ModeledResponse<MealPlanMealDb>> () => await supabaseClient
                    .From<MealPlanMealDb>()
                    .Insert(newRows));
                if (insertResult.IsFailed) return insertResult.ToResult();
            }

            return Result.Ok().WithSuccess(localizer["PlanWeek_Saved"]);
        }

        private static string Iso(DateTime date) => date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
