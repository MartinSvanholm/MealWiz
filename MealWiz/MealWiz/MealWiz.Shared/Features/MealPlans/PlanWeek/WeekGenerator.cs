using MealWiz.Shared.Features.MealPlans.Models;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Helpers;

namespace MealWiz.Shared.Features.MealPlans.PlanWeek;

public sealed record WeekGeneratorInput(
    DateTime WeekStart,
    IReadOnlyList<Meal> Pool,
    IReadOnlyCollection<PlannedMealDate> PlannedDates,
    IReadOnlyDictionary<DateTime, Meal> LockedDays,
    int WeeksBack,
    bool LeftoversAfterEveryMeal,
    Meal? Leftovers);

public sealed record GeneratedDay(DateTime Date, Meal? Meal, bool IsLocked);

public sealed record GeneratedWeek(IReadOnlyList<GeneratedDay> Days, bool UsedFallback);

/// <summary>
/// Picks a random regular meal for every unlocked day of a week. Meals used within <c>WeeksBack</c> weeks before or after
/// the week (or already in it) are skipped; when too few remain, the meals used longest ago fill the rest.
/// </summary>
public static class WeekGenerator
{
    public static GeneratedWeek Generate(WeekGeneratorInput input, Random random)
    {
        var monday = input.WeekStart.StartOfWeek().Date;
        var sunday = monday.AddDays(6);
        var from = monday.AddDays(-7 * input.WeeksBack);
        var to = sunday.AddDays(7 * input.WeeksBack);

        var locked = input.LockedDays.ToDictionary(kv => kv.Key.Date, kv => kv.Value);
        var pool = input.Pool.Where(m => m.IsRegular).DistinctBy(m => m.Id).ToList();
        var inWindow = input.PlannedDates.Where(p => p.Date.Date >= from && p.Date.Date <= to).ToList();

        var excluded = inWindow.Select(p => p.MealId).Concat(locked.Values.Select(m => m.Id)).ToHashSet();

        int Distance(int mealId)
        {
            var dates = inWindow.Where(p => p.MealId == mealId).Select(p => p.Date.Date).ToList();
            if (dates.Count == 0) return 0;
            return dates.Min(d => d < monday ? (monday - d).Days : d > sunday ? (d - sunday).Days : 0);
        }

        var fresh = Shuffle(pool.Where(m => !excluded.Contains(m.Id)), random);
        var fallback = Shuffle(pool.Where(m => excluded.Contains(m.Id)), random)
            .OrderByDescending(m => Distance(m.Id))
            .ToList();
        var queue = fresh.Concat(fallback).ToList();

        var usedThisWeek = locked.Values.Select(m => m.Id).ToHashSet();
        var next = 0;
        var repeat = 0;
        var usedFallback = false;

        Meal? TakeNext()
        {
            if (queue.Count == 0) return null;

            while (next < queue.Count)
            {
                var meal = queue[next++];
                if (usedThisWeek.Contains(meal.Id)) continue;

                if (excluded.Contains(meal.Id)) usedFallback = true;
                usedThisWeek.Add(meal.Id);
                return meal;
            }

            // Every meal is already in the week: repeat in the same order.
            usedFallback = true;
            return queue[repeat++ % queue.Count];
        }

        var leftovers = input.LeftoversAfterEveryMeal ? input.Leftovers : null;
        var previousWasCooked = false;
        var days = new List<GeneratedDay>(7);

        for (var i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);

            if (locked.TryGetValue(date, out var lockedMeal))
            {
                days.Add(new GeneratedDay(date, lockedMeal, true));
                previousWasCooked = lockedMeal.IsRegular;
                continue;
            }

            if (leftovers != null && previousWasCooked)
            {
                days.Add(new GeneratedDay(date, leftovers, false));
                previousWasCooked = false;
                continue;
            }

            var picked = TakeNext();
            days.Add(new GeneratedDay(date, picked, false));
            previousWasCooked = picked != null;
        }

        return new GeneratedWeek(days, usedFallback);
    }

    private static List<Meal> Shuffle(IEnumerable<Meal> meals, Random random)
    {
        var list = meals.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
