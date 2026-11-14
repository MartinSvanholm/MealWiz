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

            // One call, one transaction: the week's plan is found or created and every day gets its meal, or nothing
            // changes. A day that got a meal in the meantime is overwritten in place, so its row keeps its id.
            var saved = await Result.Try(async Task () => await supabaseClient.Rpc("save_generated_week", new Dictionary<string, object>
            {
                ["week_start"] = Iso(monday),
                ["days"] = request.Days
                    .OrderBy(d => d.Date)
                    .Select(d => new { date = Iso(d.Date), meal_id = d.MealId })
                    .ToList()
            }));
            if (saved.IsFailed) return saved;

            return Result.Ok().WithSuccess(localizer["PlanWeek_Saved"]);
        }

        private static string Iso(DateTime date) => date.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
