namespace MealWiz.Shared.Features.MealPlans.Models;

/// <summary>The Plan week settings, remembered on this device.</summary>
public sealed record PlanWeekSettings(int WeeksBack = PlanWeekSettings.DefaultWeeksBack, bool LeftoversAfterEveryMeal = false)
{
    public const int MinWeeksBack = 0;
    public const int MaxWeeksBack = 8;
    public const int DefaultWeeksBack = 3;

    public PlanWeekSettings Clamped() => this with { WeeksBack = Math.Clamp(WeeksBack, MinWeeksBack, MaxWeeksBack) };
}
