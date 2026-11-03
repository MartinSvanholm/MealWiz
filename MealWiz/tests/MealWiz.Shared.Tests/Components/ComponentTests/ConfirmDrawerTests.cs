using Bunit;
using MealWiz.Shared.Components.Drawers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Components.ComponentTests;

public class ConfirmDrawerTests : ComponentTestBase
{
    [Fact]
    public void Shows_the_description_it_was_opened_with()
    {
        var cut = Render<ConfirmDrawer>();

        cut.InvokeAsync(() => Drawer.OpenConfirmDrawer("Delete meal?", "Are you sure?", () => Task.CompletedTask));

        Assert.True(Drawer.IsConfirmDrawerOpen);
        Assert.Equal("Are you sure?", cut.Find(TestId("confirm-drawer-description")).TextContent);
    }

    [Fact]
    public void Confirm_runs_the_callback_it_was_opened_with()
    {
        var confirmed = 0;
        var cut = Render<ConfirmDrawer>();
        cut.InvokeAsync(() => Drawer.OpenConfirmDrawer("Delete meal?", "Are you sure?", () => { confirmed++; return Task.CompletedTask; }));

        cut.Find($"{TestId("confirm-drawer-confirm")}").Click();

        Assert.Equal(1, confirmed);
    }

    [Fact]
    public void Shows_the_title_it_was_opened_with()
    {
        var cut = Render<ConfirmDrawer>();

        cut.InvokeAsync(() => Drawer.OpenConfirmDrawer("Delete Lasagne?", "Are you sure?", () => Task.CompletedTask));

        Assert.Equal("Delete Lasagne?", cut.Find(TestId("confirm-drawer-title")).TextContent);
    }

    [Fact]
    public void Cancel_closes_without_running_the_callback()
    {
        var confirmed = 0;
        var cut = Render<ConfirmDrawer>();
        cut.InvokeAsync(() => Drawer.OpenConfirmDrawer("Delete meal?", "Are you sure?", () => { confirmed++; return Task.CompletedTask; }));

        cut.Find(TestId("confirm-drawer-cancel")).Click();

        Assert.False(Drawer.IsConfirmDrawerOpen);
        Assert.Equal(0, confirmed);
    }

    // The main drawer shares StateParameters: while closed, the confirm drawer must not grow to show its title, or the
    // closed sheet (measured when empty) peeks above the bottom of the screen.
    [Fact]
    public void Shows_no_title_or_description_while_closed()
    {
        var cut = Render<ConfirmDrawer>();

        cut.InvokeAsync(() => Drawer.OpenDrawer(new() { Title = "Add item", Description = "not mine" }));

        Assert.False(Drawer.IsConfirmDrawerOpen);
        Assert.Empty(cut.FindAll(TestId("confirm-drawer-title")));
        Assert.Empty(cut.FindAll(TestId("confirm-drawer-description")));
    }
}
