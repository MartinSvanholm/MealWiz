using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Features.GroceryList.Models;
using Xunit;
using static TheMealWorkshop.Shared.Features.GroceryCategories.Models.DefaultCategoryKey;

namespace TheMealWorkshop.Shared.Tests.Features.GroceryCategories.UnitTests.Models;

/// <summary>Learning the section order from shopping trips: what a trip is, and how far one trip moves the order.</summary>
public class SectionOrderLearnerTests
{
    private static readonly DateTime Start = new(2026, 11, 15, 10, 0, 0, DateTimeKind.Utc);

    private static GroceryItem Tick(string name, int minutes) =>
        new() { Name = name, IsPicked = true, PickedAt = Start.AddMinutes(minutes) };

    private static List<GroceryCategory> Categories(params DefaultCategoryKey[] keys) =>
        keys.Select((k, i) => new GroceryCategory { Id = i + 1, DefaultKey = k, Position = i + 1 }).ToList();

    private static List<DefaultCategoryKey?> Order(List<GroceryCategory> categories) =>
        categories.OrderBy(c => c.Position).Select(c => c.DefaultKey).ToList();

    private static List<GroceryCategory> LearnTrip(List<GroceryCategory> categories, params DefaultCategoryKey[] walk) =>
        SectionOrderLearner.Apply(categories, SectionOrderLearner.Learn(categories, walk.Select(k => categories.Single(c => c.DefaultKey == k).Id).ToList()));

    [Fact]
    public void CompleteTrips_SplitsWhereTicksAreMoreThanTwoHoursApart()
    {
        var picked = new[] { Tick("a", 0), Tick("b", 30), Tick("c", 30 + 121), Tick("d", 200) };

        var trips = SectionOrderLearner.CompleteTrips(picked, null, Start.AddHours(10), basketCleared: false);

        Assert.Equal([["a", "b"], ["c", "d"]], trips.Select(t => t.Select(i => i.Name).ToList()).ToList());
    }

    [Fact]
    public void CompleteTrips_ATripStillGoing_IsNotFinished_UnlessTheBasketIsCleared()
    {
        var picked = new[] { Tick("a", 0), Tick("b", 30) };
        var now = Start.AddMinutes(30 + 119);

        Assert.Empty(SectionOrderLearner.CompleteTrips(picked, null, now, basketCleared: false));
        Assert.Single(SectionOrderLearner.CompleteTrips(picked, null, now, basketCleared: true));
        Assert.Single(SectionOrderLearner.CompleteTrips(picked, null, now.AddMinutes(2), basketCleared: false));
    }

    [Fact]
    public void CompleteTrips_LeavesOutTicksAlreadyLearned_AndItemsStillToBuy()
    {
        var picked = new[] { Tick("old", 0), Tick("new", 10), new GroceryItem { Name = "to buy", PickedAt = Start.AddMinutes(20) } };

        var trip = Assert.Single(SectionOrderLearner.CompleteTrips(picked, Start, Start.AddHours(5), basketCleared: false));

        Assert.Equal(["new"], trip.Select(i => i.Name));
    }

    [Fact]
    public void Learn_OneOppositeTrip_DoesNotSwapTwoNeighbours_ButASecondOneDoes()
    {
        var categories = Categories(Produce, Bakery, DairyEggs, Other);

        var afterOne = LearnTrip(categories, DairyEggs, Bakery);
        Assert.Equal([Produce, Bakery, DairyEggs, Other], Order(afterOne));

        var afterTwo = LearnTrip(afterOne, DairyEggs, Bakery);
        Assert.Equal([Produce, DairyEggs, Bakery, Other], Order(afterTwo));
    }

    [Fact]
    public void Learn_ConsistentTrips_SettleOnTheWalkedOrder()
    {
        var categories = Categories(Produce, Bakery, MeatFish, DairyEggs, Frozen, Other);

        for (var trip = 0; trip < 4; trip++)
            categories = LearnTrip(categories, Frozen, DairyEggs, MeatFish, Bakery, Produce);

        Assert.Equal([Frozen, DairyEggs, MeatFish, Bakery, Produce, Other], Order(categories));
    }

    [Fact]
    public void Learn_CategoriesNotTicked_KeepTheirPlaces_AndOtherStaysLast()
    {
        var categories = Categories(Produce, Bakery, MeatFish, DairyEggs, Other);
        var other = categories.Single(c => c.IsOther).Id;

        for (var trip = 0; trip < 3; trip++)
            categories = SectionOrderLearner.Apply(categories, SectionOrderLearner.Learn(categories, [other, 4, 1]));

        // Dairy & eggs now comes before Fruit & vegetables; Bakery and Meat & fish keep their order between them.
        var order = Order(categories);
        Assert.True(order.IndexOf(DairyEggs) < order.IndexOf(Produce));
        Assert.True(order.IndexOf(Bakery) < order.IndexOf(MeatFish));
        Assert.Equal(Other, order[^1]);
    }

    [Fact]
    public void Learn_ATripThroughOneCategory_TeachesNothing()
    {
        var categories = Categories(Produce, Bakery, Other);

        var orders = SectionOrderLearner.Learn(categories, [2]);

        Assert.Equal([(1, 1, 1.0), (2, 2, 2.0), (3, 3, 3.0)], orders.Select(o => (o.CategoryId, o.Position, o.Score)));
    }
}
