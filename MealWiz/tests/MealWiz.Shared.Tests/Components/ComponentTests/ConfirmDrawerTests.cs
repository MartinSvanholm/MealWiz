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

        cut.InvokeAsync(() => Drawer.OpenConfirmDrawer("Delete meal?", "Are you sure?"));

        Assert.True(Drawer.IsConfirmDrawerOpen);
        Assert.Equal("Are you sure?", cut.Find(TestId("confirm-drawer-description")).TextContent);
    }

    [Fact]
    public void Confirm_raises_OnConfirmClick()
    {
        var confirmed = 0;
        Drawer.OnConfirmClick += () => { confirmed++; return Task.CompletedTask; };
        var cut = Render<ConfirmDrawer>();
        cut.InvokeAsync(() => Drawer.OpenConfirmDrawer("Delete meal?", "Are you sure?"));

        cut.Find($"{TestId("confirm-drawer-confirm")}").Click();

        Assert.Equal(1, confirmed);
    }
}
