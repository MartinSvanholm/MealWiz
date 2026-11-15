using MauiWASM.Tests.TestInfrastructure.E2E;
using Microsoft.Playwright;
using Xunit;

namespace MauiWASM.Tests.E2ETests;

/// <summary>
/// The grocery list in supermarket sections (MEA-137): items land in a section from their name in either language,
/// unknown names go to Other at the bottom, a section folds and stays folded after a reload, and a section goes once its
/// last item is ticked off.
/// </summary>
[Trait("Category", "E2E")]
public class GroceryCategoriesJourneyTests : E2ETestBase
{
    private ILocator Section(string key) => Page.Locator($"[data-testid=grocery-category][data-category={key}]");

    private async Task AddItemAsync(string name)
    {
        await FillAsync(Page.GetByTestId("add-grocery-item-input"), name);
        await Page.GetByTestId("add-grocery-item-button").ClickAsync();
        await Expect(Drawer.GetByTestId("grocery-item-name-input")).ToHaveValueAsync(name);
        await Drawer.GetByTestId("save-grocery-item-button").ClickAsync();
        await Expect(Row("grocery-item", name)).ToHaveCountAsync(1);
    }

    [Fact]
    public async Task GroceryList_GroupsItemsInSections_FoldsThem_AndDropsAnEmptySection()
    {
        await LoginAsync();
        await Page.GetByTestId("nav-grocery-list").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("grocerylist"));

        await AddItemAsync("Milk");
        await AddItemAsync("mælk");
        await AddItemAsync("Zorbex");

        // Milk and mælk in Dairy & eggs, the unknown word in Other, last; no empty sections
        var sections = Page.GetByTestId("grocery-category");
        await Expect(sections).ToHaveCountAsync(2);
        await Expect(Section("dairy_eggs").GetByTestId("grocery-category-name")).ToHaveTextAsync("Dairy & eggs");
        await Expect(Section("dairy_eggs").GetByTestId("grocery-category-count")).ToHaveTextAsync("2");
        await Expect(Section("other").GetByTestId("grocery-category-count")).ToHaveTextAsync("1");
        await Expect(sections.Last).ToHaveAttributeAsync("data-category", "other");

        // Fold Dairy & eggs: the rows go, the count stays, also after a reload
        await Section("dairy_eggs").GetByTestId("grocery-category-heading").ClickAsync();
        await Expect(Section("dairy_eggs").GetByTestId("grocery-item")).ToHaveCountAsync(0);
        await Expect(Section("dairy_eggs").GetByTestId("grocery-category-count")).ToHaveTextAsync("2");

        await Page.ReloadAsync();
        await Expect(Section("dairy_eggs")).ToHaveAttributeAsync("data-folded", "true");
        await Expect(Section("dairy_eggs").GetByTestId("grocery-item")).ToHaveCountAsync(0);

        await Section("dairy_eggs").GetByTestId("grocery-category-heading").ClickAsync();
        await Expect(Section("dairy_eggs").GetByTestId("grocery-item")).ToHaveCountAsync(2);

        // Tick both dairy items: the section goes and the basket holds two pills
        await Row("grocery-item", "Milk").GetByTestId("grocery-item-checkbox").ClickAsync();
        await Expect(Section("dairy_eggs").GetByTestId("grocery-category-count")).ToHaveTextAsync("1");
        await Row("grocery-item", "mælk").GetByTestId("grocery-item-checkbox").ClickAsync();
        await Expect(Section("dairy_eggs")).ToHaveCountAsync(0);
        await Expect(Page.GetByTestId("grocery-basket").GetByTestId("grocery-item")).ToHaveCountAsync(2);
    }

    [Fact]
    public async Task GroceryList_AMovedNameStaysMoved_AndARenamedCategoryShowsOnTheList()
    {
        await LoginAsync();
        await Page.GetByTestId("nav-grocery-list").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("grocerylist"));

        // An unknown word lands in Other
        await AddItemAsync("Tofu");
        await Expect(Section("other").GetByTestId("grocery-item")).ToHaveCountAsync(1);

        // Move it to Meat & fish with the chips in its drawer
        await Row("grocery-item", "Tofu").GetByTestId("grocery-item-name").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Edit item");
        await Drawer.Locator("[data-testid=category-chip][data-category=meat_fish]").ClickAsync();
        await Drawer.GetByTestId("save-grocery-item-button").ClickAsync();
        await Expect(Section("meat_fish").GetByTestId("grocery-item")).ToHaveCountAsync(1);
        await Expect(Section("other")).ToHaveCountAsync(0);

        // A later tofu lands there too
        await FillAsync(Page.GetByTestId("add-grocery-item-input"), "tofu");
        await Page.GetByTestId("add-grocery-item-button").ClickAsync();
        await Expect(Drawer.Locator("[data-testid=category-chip][data-category=meat_fish]")).ToHaveAttributeAsync("aria-pressed", "true");
        await Drawer.GetByTestId("save-grocery-item-button").ClickAsync();
        await Expect(Section("meat_fish").GetByTestId("grocery-category-count")).ToHaveTextAsync("2");

        // Rename Meat & fish on the Categories page
        await Page.GetByTestId("grocery-categories-button").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("grocerylist/categories"));
        await Page.Locator("[data-testid=grocery-category-row][data-category=meat_fish]").ClickAsync();
        await Expect(Drawer.GetByTestId("drawer-title")).ToHaveTextAsync("Edit category");
        await FillAsync(Drawer.GetByTestId("grocery-category-name-input"), "Protein");
        await FillAsync(Drawer.GetByTestId("grocery-category-emoji-input"), "🥩");
        await Drawer.GetByTestId("save-grocery-category").ClickAsync();
        await Expect(Page.Locator("[data-testid=grocery-category-row][data-category=meat_fish]")).ToHaveTextAsync("🥩 Protein");

        await Page.GetByTestId("grocery-categories-back").ClickAsync();
        await Expect(Page).ToHaveURLAsync(UrlFor("grocerylist"));
        await Expect(Section("meat_fish").GetByTestId("grocery-category-name")).ToHaveTextAsync("🥩 Protein");
    }
}
