using MealWiz.Shared.Features.GroceryList.Models;

namespace MealWiz.Shared.Features.GroceryCategories.Models;

/// <summary>A category's place after learning: its new position and its learned, fractional score.</summary>
public record LearnedCategoryOrder(int CategoryId, int Position, double Score);

/// <summary>
/// Learns the section order from shopping trips. Pure: the container feeds it the ticked-off items and the categories
/// and writes what it returns.
/// <para>
/// A trip is the ticks less than <see cref="TripGap"/> apart. In a trip, the order in which categories got their first
/// tick is what the shopper walked. The ticked categories keep the positions they hold between them (the slots), and
/// each one's score moves <see cref="Rate"/> of the way toward the slot of its rank in the walk, so an adjacent swap
/// takes two consistent trips and one odd trip doesn't scramble the list. Other is never learned and stays last.
/// </para>
/// </summary>
public static class SectionOrderLearner
{
    /// <summary>Ticks further apart than this belong to different trips; a trip is over once this has passed.</summary>
    public static readonly TimeSpan TripGap = TimeSpan.FromHours(2);

    /// <summary>How far a trip moves each ticked category toward its observed slot.</summary>
    public const double Rate = 0.35;

    /// <summary>
    /// The finished trips among the ticked-off items after <paramref name="learnedUntil"/>, oldest first, each in tick
    /// order. A trip is finished when its last tick is more than <see cref="TripGap"/> before <paramref name="now"/>, or
    /// when the basket is being cleared (which deletes the ticks).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<GroceryItem>> CompleteTrips(
        IEnumerable<GroceryItem> picked, DateTime? learnedUntil, DateTime now, bool basketCleared)
    {
        var ticks = picked
            .Where(item => item.IsPicked && item.PickedAt is { } at && (learnedUntil is null || at > learnedUntil))
            .OrderBy(item => item.PickedAt)
            .ToList();

        var trips = new List<List<GroceryItem>>();
        foreach (var tick in ticks)
        {
            if (trips.Count == 0 || tick.PickedAt!.Value - trips[^1][^1].PickedAt!.Value > TripGap)
                trips.Add([]);
            trips[^1].Add(tick);
        }

        if (!basketCleared && trips.Count > 0 && now - trips[^1][^1].PickedAt!.Value <= TripGap)
            trips.RemoveAt(trips.Count - 1);

        return trips;
    }

    /// <summary>
    /// The categories' places after one trip whose first ticks went to <paramref name="observedCategoryIds"/> in that
    /// order. Every category is in the result, Other last. Fewer than two learnable categories teach nothing: the
    /// places come back as they are.
    /// </summary>
    public static List<LearnedCategoryOrder> Learn(IReadOnlyList<GroceryCategory> categories, IReadOnlyList<int> observedCategoryIds)
    {
        var movable = categories.Where(c => !c.IsOther).OrderBy(c => c.Position).ToList();
        var scores = movable.ToDictionary(c => c.Id, c => c.OrderScore ?? c.Position);

        var observed = observedCategoryIds.Distinct().Where(scores.ContainsKey).ToList();
        if (observed.Count >= 2)
        {
            var slots = observed.Select(id => movable.First(c => c.Id == id).Position).Order().ToList();
            for (var rank = 0; rank < observed.Count; rank++)
            {
                var id = observed[rank];
                scores[id] += Rate * (slots[rank] - scores[id]);
            }
        }

        var ordered = movable
            .OrderBy(c => scores[c.Id])
            .ThenBy(c => c.Position)
            .Select((c, index) => new LearnedCategoryOrder(c.Id, index + 1, scores[c.Id]))
            .ToList();

        ordered.AddRange(categories
            .Where(c => c.IsOther)
            .Select((c, index) => new LearnedCategoryOrder(c.Id, movable.Count + index + 1, movable.Count + index + 1)));
        return ordered;
    }

    /// <summary>The categories with <paramref name="orders"/> applied, in their new order (copies; the input is unchanged).</summary>
    public static List<GroceryCategory> Apply(IReadOnlyList<GroceryCategory> categories, IReadOnlyList<LearnedCategoryOrder> orders)
    {
        var byId = orders.ToDictionary(o => o.CategoryId);
        return categories
            .Select(c =>
            {
                var copy = c.Copy();
                if (byId.TryGetValue(c.Id, out var order))
                {
                    copy.Position = order.Position;
                    copy.OrderScore = order.Score;
                }
                return copy;
            })
            .OrderBy(c => c.Position)
            .ToList();
    }
}
