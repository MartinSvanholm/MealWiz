using Bunit;
using MealWiz.Shared.Components.Drawers;
using MealWiz.Shared.Services.DrawerStateContainer;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Xunit;

namespace MealWiz.Shared.Tests.Components.ComponentTests;

public class DrawerBaseTests : ComponentTestBase
{
    [Fact]
    public void Opening_the_drawer_renders_its_title_and_content_with_parameters()
    {
        var cut = Render<DrawerBase>();

        cut.InvokeAsync(() => Drawer.OpenDrawer(ProbeDrawer("Shopping", "probe text")));

        Assert.Equal("Shopping", cut.Find(TestId("drawer-title")).TextContent);
        Assert.Equal("probe text", cut.Find(TestId("probe")).TextContent);
    }

    [Fact]
    public void Close_button_closes_the_drawer_and_clears_its_content()
    {
        var cut = Render<DrawerBase>();
        cut.InvokeAsync(() => Drawer.OpenDrawer(ProbeDrawer("Shopping", "probe text")));

        cut.Find(TestId("drawer-close")).Click();

        Assert.False(Drawer.IsDrawerOpen);
        Assert.Empty(cut.FindAll(TestId("probe")));
    }

    [Fact]
    public void Back_button_returns_to_the_previous_drawer_content()
    {
        var cut = Render<DrawerBase>();
        cut.InvokeAsync(() =>
        {
            Drawer.OpenDrawer(ProbeDrawer("First", "first"));
            Drawer.SwitchDrawerContent(typeof(Probe), "First", new() { [nameof(Probe.Text)] = "first" });
            Drawer.SwitchDrawerContent(typeof(Probe), "Second", new() { [nameof(Probe.Text)] = "second" });
        });
        Assert.Equal("second", cut.Find(TestId("probe")).TextContent);

        cut.Find(TestId("drawer-back")).Click();

        Assert.True(Drawer.IsDrawerOpen);
        Assert.Equal("First", cut.Find(TestId("drawer-title")).TextContent);
        Assert.Equal("first", cut.Find(TestId("probe")).TextContent);
    }

    private static DrawerStateParameters ProbeDrawer(string title, string text) => new()
    {
        Title = title,
        Content = typeof(Probe),
        ContentParameters = new() { [nameof(Probe.Text)] = text }
    };

    /// <summary>Stand-in drawer content, so these tests don't depend on a feature component.</summary>
    public class Probe : ComponentBase
    {
        [Parameter] public string Text { get; set; } = string.Empty;

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "p");
            builder.AddAttribute(1, "data-testid", "probe");
            builder.AddContent(2, Text);
            builder.CloseElement();
        }
    }
}
