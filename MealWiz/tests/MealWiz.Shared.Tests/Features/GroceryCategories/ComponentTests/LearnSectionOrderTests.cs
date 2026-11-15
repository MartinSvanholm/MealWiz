using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryCategories.ApplyLearnedSectionOrder;
using MealWiz.Shared.Features.GroceryList;
using MealWiz.Shared.Features.GroceryList.DeleteGroceryItems;
using MealWiz.Shared.Features.GroceryList.GetAllGroceryItems;
using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryCategories.ComponentTests;

/// <summary>The grocery list learns the section order from a finished trip on load, and from the basket on Clear.</summary>
public class LearnSectionOrderTests : ComponentTestBase
{
    // The test clock is 2026-11-04 12:00 UTC.
    private static GroceryItem Ticked(string name, int id, int hour) =>
        new() { Id = id, Name = name, IsPicked = true, PickedAt = new DateTime(2026, 11, 4, hour, 0, 0, DateTimeKind.Utc) };

    public LearnSectionOrderTests()
    {
        SetupSend<ApplyLearnedSectionOrder.Command, Result>(Result.Ok());
        SetupSend<DeleteGroceryItems.Command, Result>(Result.Ok());
    }

    private void HaveItems(params GroceryItem[] items) =>
        SetupSend<GetAllGroceryItems.Query, Result<List<GroceryItem>>>(Result.Ok(items.ToList()));

    [Fact]
    public void OnLoad_ATripThatEndedMoreThanTwoHoursAgo_IsLearned()
    {
        HaveItems(Ticked("Milk", 1, 8), Ticked("Bread", 2, 9));

        RenderWithPopovers<GroceryListPage>();

        var command = Assert.Single(Sent<ApplyLearnedSectionOrder.Command>());
        Assert.Equal(new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc), command.LearnedUntil);
    }

    [Fact]
    public void OnLoad_ATripStillGoing_IsNotLearnedYet()
    {
        HaveItems(Ticked("Milk", 1, 11), Ticked("Bread", 2, 11));

        RenderWithPopovers<GroceryListPage>();

        Assert.Empty(Sent<ApplyLearnedSectionOrder.Command>());
    }

    [Fact]
    public void ClearingTheBasket_LearnsTheTrip_BeforeDeletingTheTicks()
    {
        HaveItems(Ticked("Milk", 1, 11), Ticked("Bread", 2, 11));
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryListPage>();

        cut.Find(TestId("clear-basket-button")).Click();
        Confirm();

        cut.WaitForAssertion(() => Assert.Single(Sent<DeleteGroceryItems.Command>()));
        var requests = SentRequests.ToList();
        Assert.True(requests.FindIndex(r => r is ApplyLearnedSectionOrder.Command) < requests.FindIndex(r => r is DeleteGroceryItems.Command));
    }
}
