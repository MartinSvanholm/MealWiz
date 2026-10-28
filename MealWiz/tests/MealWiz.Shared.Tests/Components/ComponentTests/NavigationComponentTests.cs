using Bunit;
using MealWiz.Shared.Components;
using MealWiz.Shared.Components.Card;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace MealWiz.Shared.Tests.Components.ComponentTests;

/// <summary>NavMenu, CustomNavLink and BaseCard: the clickable building blocks of the app shell.</summary>
public class NavigationComponentTests : ComponentTestBase
{
    [Theory]
    [InlineData("nav-meal-plan", "")]
    [InlineData("nav-grocery-list", "grocerylist")]
    [InlineData("nav-meals", "meals")]
    public void NavMenu_link_navigates_to_its_page(string testId, string expectedPath)
    {
        Navigation.NavigateTo("/account");
        var cut = Render<NavMenu>();

        cut.Find(TestId(testId)).Click();

        Assert.Equal(expectedPath, CurrentPath);
    }

    // MEA-48: the menu had a Freezer link to /freezer, which no page declares.
    [Fact]
    public void NavMenu_links_only_to_pages_that_exist()
    {
        var pageRoutes = typeof(NavMenu).Assembly.GetTypes()
            .SelectMany(type => type.GetCustomAttributes(typeof(RouteAttribute), inherit: false).Cast<RouteAttribute>())
            .Select(route => route.Template.Trim('/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Navigation.NavigateTo("/account");
        var cut = Render<NavMenu>();
        var links = cut.FindAll("[data-testid^=\"nav-\"]");

        Assert.NotEmpty(links);
        foreach (var link in links)
        {
            link.Click();
            Assert.True(pageRoutes.Contains(CurrentPath), $"{link.GetAttribute("data-testid")} goes to /{CurrentPath}, which has no page");
        }
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
