using Bunit;
using MealWiz.Shared.Components;
using MealWiz.Shared.Components.Card;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Components.ComponentTests;

/// <summary>NavMenu, CustomNavLink and BaseCard: the clickable building blocks of the app shell.</summary>
public class NavigationComponentTests : ComponentTestBase
{
    [Theory]
    [InlineData("nav-meal-plan", "")]
    [InlineData("nav-grocery-list", "grocerylist")]
    [InlineData("nav-meals", "meals")]
    [InlineData("nav-freezer", "freezer")]
    public void NavMenu_link_navigates_to_its_page(string testId, string expectedPath)
    {
        Navigation.NavigateTo("/account");
        var cut = Render<NavMenu>();

        cut.Find(TestId(testId)).Click();

        Assert.Equal(expectedPath, CurrentPath);
    }

    [Fact]
    public void NavMenu_marks_the_current_page_active_and_follows_navigation()
    {
        Navigation.NavigateTo("/meals");
        var cut = Render<NavMenu>();

        Assert.Contains("active", cut.Find(TestId("nav-meals")).ClassList);
        Assert.DoesNotContain("active", cut.Find(TestId("nav-grocery-list")).ClassList);

        cut.InvokeAsync(() => Navigation.NavigateTo("/grocerylist"));

        Assert.Contains("active", cut.Find(TestId("nav-grocery-list")).ClassList);
        Assert.DoesNotContain("active", cut.Find(TestId("nav-meals")).ClassList);
    }

    [Fact]
    public void CustomNavLink_renders_its_content_and_navigates_on_click()
    {
        var cut = Render<CustomNavLink>(p => p
            .Add(l => l.Href, "/meals")
            .Add(l => l.Childcontent, "Meals")
            .AddUnmatched("data-testid", "link"));

        Assert.Contains("Meals", cut.Find(TestId("link")).TextContent);

        cut.Find(TestId("link")).Click();

        Assert.Equal("meals", CurrentPath);
    }

    [Fact]
    public void BaseCard_renders_header_and_content_and_invokes_OnClick()
    {
        var clicks = 0;
        var cut = Render<BaseCard>(p => p
            .Add(c => c.HeaderContent, "Header text")
            .Add(c => c.Content, "Body text")
            .Add(c => c.OnClick, () => clicks++)
            .AddUnmatched("data-testid", "card"));

        Assert.Contains("Header text", cut.Markup);
        Assert.Contains("Body text", cut.Markup);

        cut.Find(TestId("card")).Click();

        Assert.Equal(1, clicks);
    }
}
