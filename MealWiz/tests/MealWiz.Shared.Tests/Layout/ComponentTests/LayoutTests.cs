using Bunit;
using MealWiz.Shared.Layout;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Layout.ComponentTests;

public class LayoutTests : ComponentTestBase
{
    [Fact]
    public void RedirectToLogin_sends_the_user_to_login()
    {
        Navigation.NavigateTo("/meals");

        Render<RedirectToLogin>();

        Assert.Equal("login", CurrentPath);
    }

    [Fact]
    public void MainLayout_renders_the_page_and_the_nav_menu_and_loads_the_theme()
    {
        RenderFragment body = builder => builder.AddMarkupContent(0, "<p data-testid=\"page-body\">Page body</p>");

        var cut = Render<MainLayout>(p => p.Add(l => l.Body, body));

        Assert.Equal("Page body", cut.Find(TestId("page-body")).TextContent);
        Assert.NotNull(cut.Find(TestId("nav-meals")));
        Theme.Verify(t => t.InitializeAsync(), Times.Once);
    }

    [Fact]
    public void MainLayout_hosts_the_drawers()
    {
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));

        cut.InvokeAsync(() => Drawer.OpenConfirmDrawer("Delete?", "Really delete?"));

        Assert.Equal("Really delete?", cut.Find(TestId("confirm-drawer-description")).TextContent);
    }
}
