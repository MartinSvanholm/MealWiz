using Bunit;
using FluentResults;
using TheMealWorkshop.Shared.Features.GroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.AddGroceryCategory;
using TheMealWorkshop.Shared.Features.GroceryCategories.GetGroceryCategories;
using TheMealWorkshop.Shared.Features.GroceryCategories.Models;
using TheMealWorkshop.Shared.Features.GroceryCategories.UpdateGroceryCategory;
using TheMealWorkshop.Shared.Features.GroceryList;
using TheMealWorkshop.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace TheMealWorkshop.Shared.Tests.Features.GroceryCategories.ComponentTests;

/// <summary>The Categories page: the categories in order, adding one and renaming one in the category drawer.</summary>
public class GroceryCategoriesPageTests : ComponentTestBase
{
    private List<GroceryCategory> _categories = TestData.DefaultGroceryCategories();

    public GroceryCategoriesPageTests()
    {
        Mediator.Setup(m => m.Send(It.IsAny<GetGroceryCategories.Query>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Ok(_categories.OrderBy(c => c.Position).ToList()));
    }

    private IRenderedComponent<GroceryCategoriesPage> RenderPage()
    {
        RenderDrawers();
        return RenderWithPopovers<GroceryCategoriesPage>();
    }

    private static List<string> RowNames(IRenderedComponent<GroceryCategoriesPage> cut) =>
        cut.FindAll(TestId("grocery-category-row-name")).Select(n => n.TextContent.Trim()).ToList();

    private void OpenRow(IRenderedComponent<GroceryCategoriesPage> cut, string key) =>
        cut.FindAll(TestId("grocery-category-row")).Single(r => r.GetAttribute("data-category") == key).Click();

    private void Fill(string testId, string value) => DrawerHost.Find(InputOf(testId)).Change(value);

    [Fact]
    public void ListsTheCategoriesInOrder_WithABackButtonToTheGroceryList()
    {
        var cut = RenderPage();

        var names = RowNames(cut);
        Assert.Equal(11, names.Count);
        Assert.Equal("Fruit & vegetables", names[0]);
        Assert.Equal("Other", names[^1]);
        cut.Find(TestId("grocery-categories-back")).Click();
        Assert.Equal("grocerylist", CurrentPath);
    }

    [Fact]
    public void TheGroceryListHeader_LinksToTheCategoriesPage()
    {
        var cut = RenderWithPopovers<GroceryListPage>();

        Assert.Equal("grocerylist/categories", cut.Find(TestId("grocery-categories-button")).GetAttribute("href"));
    }

    [Fact]
    public void AddingACategory_SendsItsNameAndEmoji_AndShowsItAboveOther()
    {
        Mediator.Setup(m => m.Send(It.IsAny<AddGroceryCategory.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AddGroceryCategory.Command c, CancellationToken _) =>
            {
                var added = new GroceryCategory { Id = 12, Name = c.Name, Emoji = c.Emoji, Position = 11 };
                _categories.Single(x => x.IsOther).Position = 12;
                _categories.Add(added);
                return Result.Ok(added);
            });
        var cut = RenderPage();

        cut.Find(TestId("add-grocery-category")).Click();
        Fill("grocery-category-name-input", "Health food");
        Fill("grocery-category-emoji-input", "🥗");
        DrawerHost.Find(TestId("save-grocery-category")).Click();

        cut.WaitForAssertion(() => Assert.Equal("🥗 Health food", RowNames(cut)[^2]));
        Assert.Equal(("Health food", "🥗"), (Sent<AddGroceryCategory.Command>()[0].Name, Sent<AddGroceryCategory.Command>()[0].Emoji));
        Assert.False(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void RenamingADefault_StartsFromItsName_AndShowsTheTypedNameInEitherLanguage()
    {
        Mediator.Setup(m => m.Send(It.IsAny<UpdateGroceryCategory.Command>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateGroceryCategory.Command c, CancellationToken _) =>
            {
                var meat = _categories.Single(x => x.Id == c.Id);
                (meat.Name, meat.Emoji) = (c.Name, c.Emoji);
                return Result.Ok(meat);
            });
        var cut = RenderPage();

        OpenRow(cut, "meat_fish");
        Assert.Equal("Meat & fish", DrawerHost.Find(InputOf("grocery-category-name-input")).GetAttribute("value"));
        Fill("grocery-category-name-input", "Protein");
        Fill("grocery-category-emoji-input", "🥩");
        DrawerHost.Find(TestId("save-grocery-category")).Click();

        cut.WaitForAssertion(() => Assert.Equal("🥩 Protein", RowNames(cut)[2]));
        var command = Assert.Single(Sent<UpdateGroceryCategory.Command>());
        Assert.Equal((3, "Protein", "🥩"), (command.Id, command.Name, command.Emoji));
        using var _ = CultureScope.Use("da");
        Assert.Equal("🥩 Protein", GroceryCategories.Categories[2].DisplayName(TestLocalizer.For<GroceryCategoriesResource>()));
    }

    [Theory]
    [InlineData("  ", "Give the category a name")]
    [InlineData("bakery", "A category with that name already exists")]
    public void AnEmptyOrTakenName_ShowsTheErrorAndSendsNothing(string name, string error)
    {
        var cut = RenderPage();

        OpenRow(cut, "meat_fish");
        Fill("grocery-category-name-input", name);
        DrawerHost.Find(TestId("save-grocery-category")).Click();

        Assert.Equal(error, DrawerHost.Find(TestId("grocery-category-name-error")).TextContent.Trim());
        Assert.Empty(Sent<UpdateGroceryCategory.Command>());
        Assert.True(Drawer.IsDrawerOpen);
    }

    [Fact]
    public void AFailedSave_ShowsTheError_AndKeepsTheDrawerOpen()
    {
        SetupSend<UpdateGroceryCategory.Command, Result<GroceryCategory>>(Result.Fail<GroceryCategory>("No response from server"));
        var cut = RenderPage();

        OpenRow(cut, "meat_fish");
        Fill("grocery-category-name-input", "Protein");
        DrawerHost.Find(TestId("save-grocery-category")).Click();

        cut.WaitForAssertion(() => Assert.Contains("No response from server", SnackbarMessages));
        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("Meat & fish", RowNames(cut)[2]);
    }
}
