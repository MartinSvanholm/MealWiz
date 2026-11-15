using Bunit;
using FluentResults;
using MealWiz.Shared.Features.GroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.DeleteGroceryCategory;
using MealWiz.Shared.Features.GroceryCategories.GetGroceryCategories;
using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryCategories.ComponentTests;

/// <summary>Deleting a category from the Categories page: pick who takes over its items, then Delete.</summary>
public class DeleteGroceryCategoryTests : ComponentTestBase
{
    private readonly List<GroceryCategory> _categories = TestData.DefaultGroceryCategories();

    public DeleteGroceryCategoryTests()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetGroceryCategories.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Ok(_categories.ToList()));
        Mediator.Setup(m => m.Send(It.IsAny<DeleteGroceryCategory.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeleteGroceryCategory.Command c, CancellationToken _) =>
            {
                _categories.RemoveAll(x => x.Id == c.Id);
                return Result.Ok();
            });
    }

    private IRenderedComponent<GroceryCategoriesPage> RenderAndOpen(string key)
    {
        RenderDrawers();
        var cut = RenderWithPopovers<GroceryCategoriesPage>();
        cut.FindAll(TestId("grocery-category-row")).Single(r => r.GetAttribute("data-category") == key).Click();
        return cut;
    }

    private void Tap(string key) =>
        DrawerHost.FindAll(TestId("category-chip")).Single(c => c.GetAttribute("data-category") == key).Click();

    [Fact]
    public void Other_HasNoDeleteAction()
    {
        RenderAndOpen("other");

        Assert.Empty(DrawerHost.FindAll(TestId("delete-grocery-category")));
    }

    [Fact]
    public void Delete_AsksWhoTakesOver_WithTheOtherCategoriesAndDeleteOffUntilOneIsPicked()
    {
        RenderAndOpen("bakery");

        DrawerHost.Find(TestId("delete-grocery-category")).Click();

        Assert.Equal("Delete Bakery?", DrawerHost.Find(TestId("drawer-title")).TextContent.Trim());
        var chips = DrawerHost.FindAll(TestId("category-chip")).Select(c => c.GetAttribute("data-category")).ToList();
        Assert.Equal(10, chips.Count);
        Assert.DoesNotContain("bakery", chips);
        Assert.Contains("other", chips);
        Assert.True(DrawerHost.Find(TestId("confirm-delete-grocery-category")).HasAttribute("disabled"));
    }

    [Fact]
    public void PickingATakeoverAndDeleting_SendsTheCommand_AndTheRowGoes()
    {
        var cut = RenderAndOpen("bakery");
        DrawerHost.Find(TestId("delete-grocery-category")).Click();

        Tap("pantry");
        DrawerHost.Find(TestId("confirm-delete-grocery-category")).Click();

        var command = Assert.Single(Sent<DeleteGroceryCategory.Command>());
        Assert.Equal((2, 7), (command.Id, command.MoveToId));
        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll(TestId("grocery-category-row")).Count));
        Assert.False(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void AFailedDelete_ShowsTheError_AndKeepsTheCategory()
    {
        SetupSend<DeleteGroceryCategory.Command, Result>(Result.Fail("No response from server"));
        var cut = RenderAndOpen("bakery");
        DrawerHost.Find(TestId("delete-grocery-category")).Click();

        Tap("pantry");
        DrawerHost.Find(TestId("confirm-delete-grocery-category")).Click();

        cut.WaitForAssertion(() => Assert.Contains("No response from server", SnackbarMessages));
        Assert.Equal(11, cut.FindAll(TestId("grocery-category-row")).Count);
        Assert.True(Drawer.IsDrawerOpen);
    }
}
