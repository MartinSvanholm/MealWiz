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
}
