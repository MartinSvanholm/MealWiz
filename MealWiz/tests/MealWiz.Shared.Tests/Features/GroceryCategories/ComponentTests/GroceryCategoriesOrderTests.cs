using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.GetGroceryListSettings;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Features.GroceryCategories.ReorderGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.SetSectionOrderMode;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components.Web;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryCategories.ComponentTests;

/// <summary>Dragging the categories into an order on the Categories page, and handing the order back to learning.</summary>
public class GroceryCategoriesOrderTests : ComponentTestBase
{
    private List<GroceryCategory> _categories = TestData.DefaultGroceryCategories();
    private GroceryListSettings _settings = new();

    public GroceryCategoriesOrderTests()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetGroceryCategories.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Ok(_categories.OrderBy(c => c.Position).ToList()));
        Mediator.Setup(m => m.Send(It.IsAny<GetGroceryListSettings.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Ok(_settings));
        Mediator.Setup(m => m.Send(It.IsAny<ReorderGroceryCategories.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ReorderGroceryCategories.Command c, CancellationToken _) =>
            {
                for (var i = 0; i < c.CategoryIds.Count; i++) _categories.Single(x => x.Id == c.CategoryIds[i]).Position = i + 1;
                _settings = _settings with { OrderMode = SectionOrderMode.Manual };
                return Result.Ok();
            });
    }

    private static List<string?> Keys(IRenderedComponent<GroceryCategoriesPage> cut) =>
        cut.FindAll(TestId("grocery-category-row")).Select(r => r.GetAttribute("data-category")).ToList();

    // Press the row's handle, move by dy pixels over the list, and let go there.
    private static void Drag(IRenderedComponent<GroceryCategoriesPage> cut, string key, double dy, string pointerType = "touch")
    {
        var handle = cut.FindAll(TestId("grocery-category-drag-handle"))
            .Single(h => h.ParentElement!.QuerySelector(TestId("grocery-category-row"))!.GetAttribute("data-category") == key);
        handle.PointerDown(new PointerEventArgs { PointerId = 7, PointerType = pointerType, ClientY = 100 });
        cut.Find(TestId("grocery-categories-list")).PointerMove(new PointerEventArgs { PointerId = 7, PointerType = pointerType, ClientY = 100 + dy });
        cut.Find(TestId("grocery-categories-list")).PointerUp(new PointerEventArgs { PointerId = 7, PointerType = pointerType, ClientY = 100 + dy });
    }

    [Fact]
    public void EveryCategoryButOther_HasADragHandle()
    {
        var cut = RenderWithPopovers<GroceryCategoriesPage>();

        Assert.Equal(10, cut.FindAll(TestId("grocery-category-drag-handle")).Count);
        var otherRow = cut.Find("[data-testid=grocery-category-row][data-category=other]");
        Assert.Null(otherRow.ParentElement!.QuerySelector(TestId("grocery-category-drag-handle")));
    }

    [Theory]
    [InlineData("touch")]
    [InlineData("mouse")]
    public void DraggingARowDownTwoRows_SavesTheNewOrder_WithOtherLast(string pointerType)
    {
        var cut = RenderWithPopovers<GroceryCategoriesPage>();

        Drag(cut, "produce", 2 * GroceryCategoriesPage.RowHeight + 10, pointerType);

        var command = Assert.Single(Sent<ReorderGroceryCategories.Command>());
        Assert.Equal([2, 3, 1, 4, 5, 6, 7, 8, 9, 10, 11], command.CategoryIds);
        cut.WaitForAssertion(() => Assert.Equal("produce", Keys(cut)[2]));
        Assert.Equal(SectionOrderMode.Manual, GroceryCategories.Settings.OrderMode);
    }

    [Fact]
    public void DraggingPastOther_StopsJustAboveIt()
    {
        var cut = RenderWithPopovers<GroceryCategoriesPage>();

        Drag(cut, "bakery", 20 * GroceryCategoriesPage.RowHeight);

        var ids = Assert.Single(Sent<ReorderGroceryCategories.Command>()).CategoryIds;
        Assert.Equal([2, 11], ids[^2..]);
    }

    [Fact]
    public void ADropWhereItStarted_SendsNothing()
    {
        var cut = RenderWithPopovers<GroceryCategoriesPage>();

        Drag(cut, "bakery", 20);

        Assert.Empty(Sent<ReorderGroceryCategories.Command>());
        Assert.Equal("false", cut.Find(TestId("grocery-categories-list")).GetAttribute("data-dragging"));
    }

    [Fact]
    public void AFailedSave_ShowsTheError_AndPutsTheOldOrderBack()
    {
        SetupSend<ReorderGroceryCategories.Command, Result>(Result.Fail("No response from server"));
        var cut = RenderWithPopovers<GroceryCategoriesPage>();

        Drag(cut, "produce", 2 * GroceryCategoriesPage.RowHeight);

        cut.WaitForAssertion(() => Assert.Contains("No response from server", SnackbarMessages));
        Assert.Equal("produce", Keys(cut)[0]);
        Assert.Equal(SectionOrderMode.Learned, GroceryCategories.Settings.OrderMode);
    }

    private static bool SwitchIsOn(IRenderedComponent<GroceryCategoriesPage> cut) =>
        cut.Find(InputOf("follow-shopping-switch")).HasAttribute("checked");

    [Fact]
    public void TheSwitch_ShowsWhetherTheOrderIsLearned()
    {
        _settings = new GroceryListSettings(SectionOrderMode.Manual);

        var cut = RenderWithPopovers<GroceryCategoriesPage>();

        Assert.False(SwitchIsOn(cut));
        Assert.Equal("Follow my shopping again", cut.Find(TestId("follow-shopping-switch")).Closest(".grocery-categories-order")!
            .QuerySelector(".grocery-categories-order-label")!.TextContent.Trim());
    }

    [Fact]
    public void TurningTheSwitchOn_HandsTheOrderBackToLearning()
    {
        _settings = new GroceryListSettings(SectionOrderMode.Manual);
        SetupSend<SetSectionOrderMode.Command, Result>(Result.Ok());
        var cut = RenderWithPopovers<GroceryCategoriesPage>();

        cut.Find(InputOf("follow-shopping-switch")).Change(true);

        Assert.Equal(SectionOrderMode.Learned, Assert.Single(Sent<SetSectionOrderMode.Command>()).Mode);
        cut.WaitForAssertion(() => Assert.True(SwitchIsOn(cut)));
    }

    [Fact]
    public void ADrag_TurnsTheSwitchOff()
    {
        var cut = RenderWithPopovers<GroceryCategoriesPage>();
        Assert.True(SwitchIsOn(cut));

        Drag(cut, "produce", 2 * GroceryCategoriesPage.RowHeight);

        cut.WaitForAssertion(() => Assert.False(SwitchIsOn(cut)));
    }

    [Fact]
    public void AFailedSwitch_ShowsTheError_AndPutsTheSwitchBack()
    {
        SetupSend<SetSectionOrderMode.Command, Result>(Result.Fail("No response from server"));
        var cut = RenderWithPopovers<GroceryCategoriesPage>();

        cut.Find(InputOf("follow-shopping-switch")).Change(false);

        cut.WaitForAssertion(() => Assert.Contains("No response from server", SnackbarMessages));
        Assert.True(SwitchIsOn(cut));
        Assert.Equal(SectionOrderMode.Learned, GroceryCategories.Settings.OrderMode);
    }
}
