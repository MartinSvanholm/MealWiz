using MealWiz.Shared.Helpers;
using MealWiz.Shared.Localization;
using Microsoft.Extensions.Localization;

namespace MealWiz.Shared.Features.MealPlans;

public static class WeekTitles
{
    /// <summary>
    /// "This week", "Next week", "Last week", "In 3 weeks" or "3 weeks ago" ("Denne uge", "Om 3 uger", …), relative
    /// to <paramref name="today"/>'s week. Weeks start on Monday.
    /// </summary>
    public static string ToRelativeWeekTitle(this DateTime weekStart, DateTime today, IStringLocalizer<MealPlansResource> localizer)
    {
        var weeks = (int)Math.Round((weekStart.StartOfWeek().Date - today.StartOfWeek().Date).TotalDays / 7);
        return weeks switch
        {
            0 => localizer["WeekTitle_This"],
            1 => localizer["WeekTitle_Next"],
            -1 => localizer["WeekTitle_Last"],
            > 1 => localizer.Plural("WeekTitle_InWeeks", weeks),
            _ => localizer.Plural("WeekTitle_WeeksAgo", -weeks),
        };
    }
}
