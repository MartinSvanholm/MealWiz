using Bunit;
using MealWiz.Shared.Components.Headers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace MealWiz.Shared.Tests.Components.ComponentTests;

/// <summary>ScreenHeader and SubPageHeader: the label, title and action slots every page uses.</summary>
public class HeaderTests : ComponentTestBase
{
    private static RenderFragment Markup(string html) => b => b.AddMarkupContent(0, html);

    [Fact]
    public void ScreenHeader_renders_label_title_and_actions()
    {
        var cut = Render<ScreenHeader>(p => p
            .Add(h => h.Label, "Week 45 · November")
            .Add(h => h.Title, "This week")
            .Add(h => h.Actions, Markup("<button data-testid=\"probe-action\">Go</button>")));

        Assert.Equal("Week 45 · November", cut.Find(TestId("screen-header-label")).TextContent);
        Assert.Equal("This week", cut.Find(TestId("screen-header-title")).TextContent);
        Assert.NotNull(cut.Find($"{TestId("screen-header-actions")} {TestId("probe-action")}"));
    }

    [Fact]
    public void ScreenHeader_leaves_out_the_label_and_actions_when_not_set()
    {
        var cut = Render<ScreenHeader>(p => p.Add(h => h.Title, "Groceries"));

        Assert.Empty(cut.FindAll(TestId("screen-header-label")));
        Assert.Empty(cut.FindAll(TestId("screen-header-actions")));
    }

    [Fact]
    public void ScreenHeader_TitleContent_replaces_the_title_text()
    {
        var cut = Render<ScreenHeader>(p => p
            .Add(h => h.Title, "ignored")
            .Add(h => h.TitleContent, Markup("<span data-testid=\"probe-title\">Week 46</span>")));

        var title = cut.Find(TestId("screen-header-title"));
        Assert.Equal("Week 46", title.TextContent);
        Assert.NotNull(title.QuerySelector(TestId("probe-title")));
    }

    [Fact]
    public void SubPageHeader_back_button_navigates_to_BackHref()
    {
        Navigation.NavigateTo("/account");
        var cut = Render<SubPageHeader>(p => p
            .Add(h => h.Label, "Profile")
            .Add(h => h.Title, "Account")
            .Add(h => h.BackHref, "/"));

        Assert.Equal("Profile", cut.Find(TestId("screen-header-label")).TextContent);
        Assert.Equal("Account", cut.Find(TestId("screen-header-title")).TextContent);

        cut.Find(TestId("sub-page-back")).Click();

        Assert.Equal("", CurrentPath);
    }

    [Fact]
    public void SubPageHeader_OnBack_wins_over_BackHref_and_BackTestId_renames_the_button()
    {
        var backs = 0;
        Navigation.NavigateTo("/meals/import");
        var cut = Render<SubPageHeader>(p => p
            .Add(h => h.Title, "Import meal")
            .Add(h => h.BackHref, "/")
            .Add(h => h.OnBack, () => backs++)
            .Add(h => h.BackTestId, "import-back")
            .Add(h => h.Actions, Markup("<button data-testid=\"probe-action\">Go</button>")));

        cut.Find(TestId("import-back")).Click();

        Assert.Equal(1, backs);
        Assert.Equal("meals/import", CurrentPath);
        Assert.NotNull(cut.Find(TestId("probe-action")));
    }
}
