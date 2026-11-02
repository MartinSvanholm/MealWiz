using AngleSharp.Dom;
using Bunit;
using MealWiz.Shared.Layout;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components;
using Moq;
using MudBlazor;
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
        Assert.NotNull(cut.Find($"{TestId("bottom-nav")} {TestId("nav-meals")}"));
        Theme.Verify(t => t.InitializeAsync(), Times.Once);
    }

    [Theory]
    [InlineData(false, "light")]
    [InlineData(true, "dark")]
    public void MainLayout_sets_the_theme_mode_hook_from_the_theme_service(bool isDarkMode, string expected)
    {
        Theme.SetupGet(t => t.IsDarkMode).Returns(isDarkMode);

        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));

        Assert.Equal(expected, cut.Find(TestId("theme-root")).GetAttribute("data-theme"));
    }

    [Fact]
    public void MainLayout_flips_the_mode_hook_and_MudBlazor_together_when_the_theme_changes()
    {
        var isDark = false;
        Theme.SetupGet(t => t.IsDarkMode).Returns(() => isDark);
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));

        isDark = true;
        cut.InvokeAsync(() => Theme.Raise(t => t.OnChange += null));

        Assert.Equal("dark", cut.Find(TestId("theme-root")).GetAttribute("data-theme"));
        Assert.True(cut.FindComponent<MudThemeProvider>().Instance.IsDarkMode);
    }

    [Fact]
    public void MainLayout_wraps_the_providers_in_the_mode_hook_and_ignores_the_system_theme()
    {
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));

        var root = cut.Find(TestId("theme-root"));
        Assert.False(cut.FindComponent<MudThemeProvider>().Instance.ObserveSystemThemeChange);
        // Popovers (menus, selects, drawer overlays) and snackbars render inside their providers, so the providers
        // must sit inside the wrapper for the --mw-* tokens to reach them.
        AssertRendersInside(root, cut.FindComponent<MudPopoverProvider>().Markup);
        AssertRendersInside(root, cut.FindComponent<MudSnackbarProvider>().Markup);
    }

    private static void AssertRendersInside(IElement root, string providerMarkup)
    {
        Assert.False(string.IsNullOrWhiteSpace(providerMarkup));
        Assert.Contains(providerMarkup.Trim(), root.InnerHtml);
    }

    [Fact]
    public void MainLayout_hosts_the_drawers()
    {
        var cut = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(_ => { })));

        cut.InvokeAsync(() => Drawer.OpenConfirmDrawer("Delete?", "Really delete?", () => Task.CompletedTask));

        Assert.Equal("Really delete?", cut.Find(TestId("confirm-drawer-description")).TextContent);
    }
}
