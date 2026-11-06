using Bunit;
using MealWiz.Shared.Components;
using MealWiz.Shared.Components.Drawers;
using MealWiz.Shared.Components.Headers;
using MealWiz.Shared.Layout;
using MealWiz.Shared.Services.DrawerStateContainer;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace MealWiz.Shared.Tests.Components.ComponentTests;

/// <summary>
/// The app shell's texts come from the shared resource (UI-03): every localizer here echoes its key as <c>[Key]</c>,
/// so a hard-coded text would fail these tests. The English texts themselves are asserted by the other shell tests.
/// </summary>
public class ShellTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Theory]
    [InlineData("nav-meal-plan", "[Nav_Plan]")]
    [InlineData("nav-grocery-list", "[Nav_Groceries]")]
    [InlineData("nav-meals", "[Nav_Meals]")]
    public void NavMenu_Labels(string testId, string expected)
    {
        var cut = Render<NavMenu>();

        Assert.Equal(expected, cut.Find(TestId(testId)).TextContent.Trim());
    }

    [Fact]
    public void MainLayout_NavLandmarkLabel()
    {
        Auth.SetAuthorized("someone@example.com");

        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));

        Assert.Equal("[Nav_Main]", cut.Find(TestId("bottom-nav")).GetAttribute("aria-label"));
    }

    [Fact]
    public void MainLayout_SetsTheHtmlLangToTheUiLanguage()
    {
        Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));

        var call = Assert.Single(JSInterop.Invocations["document.documentElement.setAttribute"]);
        Assert.Equal(["lang", "en"], call.Arguments);
    }

    [Fact]
    public void DrawerBase_CloseAndBackLabels()
    {
        var cut = Render<DrawerBase>();
        cut.InvokeAsync(() => Drawer.OpenDrawer(Probe("First")));
        Assert.Equal("[Common_Close]", cut.Find(TestId("drawer-close")).GetAttribute("aria-label"));

        cut.InvokeAsync(() =>
        {
            Drawer.SwitchDrawerContent(typeof(DrawerBaseTests.Probe), "First", new() { [nameof(DrawerBaseTests.Probe.Text)] = "first" });
            Drawer.SwitchDrawerContent(typeof(DrawerBaseTests.Probe), "Second", new() { [nameof(DrawerBaseTests.Probe.Text)] = "second" });
        });

        Assert.Equal("[Common_Back]", cut.Find(TestId("drawer-back")).GetAttribute("aria-label"));
    }

    [Fact]
    public void ConfirmDrawer_Buttons()
    {
        var cut = Render<ConfirmDrawer>();
        cut.InvokeAsync(() => Drawer.OpenConfirmDrawer("Delete meal?", "Are you sure?", () => Task.CompletedTask));

        Assert.Equal("[Common_Cancel]", cut.Find(TestId("confirm-drawer-cancel")).TextContent.Trim());
        Assert.Equal("[Common_Delete]", cut.Find(TestId("confirm-drawer-confirm")).TextContent.Trim());
    }

    [Fact]
    public void SubPageHeader_BackLabel()
    {
        var cut = Render<SubPageHeader>(p => p.Add(h => h.Title, "Account"));

        Assert.Equal("[Common_Back]", cut.Find(TestId("sub-page-back")).GetAttribute("aria-label"));
    }

    [Fact]
    public void Routes_NotFoundMessage()
    {
        Navigation.NavigateTo("/no-such-page");

        var cut = Render<Routes>();

        Assert.Equal("[Route_NotFoundMessage]", cut.Find("[role=alert]").TextContent.Trim());
    }

    private static DrawerStateParameters Probe(string title) => new()
    {
        Title = title,
        Content = typeof(DrawerBaseTests.Probe),
        ContentParameters = new() { [nameof(DrawerBaseTests.Probe.Text)] = title },
    };
}
