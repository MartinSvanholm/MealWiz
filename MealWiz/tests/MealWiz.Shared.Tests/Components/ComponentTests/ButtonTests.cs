using Bunit;
using MealWiz.Shared.Components.Form;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Components.ComponentTests;

public class ButtonTests : ComponentTestBase
{
    [Fact]
    public void Renders_child_content()
    {
        var cut = Render<Button>(p => p.AddChildContent("Save"));

        Assert.Contains("Save", cut.Find("button").TextContent);
    }

    [Fact]
    public void Click_invokes_OnClick()
    {
        var clicks = 0;
        var cut = Render<Button>(p => p
            .AddChildContent("Save")
            .Add(b => b.OnClick, () => clicks++));

        cut.Find("button").Click();

        Assert.Equal(1, clicks);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Loading_or_disabled_button_is_disabled(bool isLoading, bool isDisabled)
    {
        var cut = Render<Button>(p => p
            .AddChildContent("Save")
            .Add(b => b.IsLoading, isLoading)
            .Add(b => b.IsDisabled, isDisabled));

        Assert.True(cut.Find("button").HasAttribute("disabled"));
    }

    [Fact]
    public void Loading_button_hides_its_content()
    {
        var cut = Render<Button>(p => p
            .AddChildContent("Save")
            .Add(b => b.IsLoading, true));

        Assert.DoesNotContain("Save", cut.Find("button").TextContent);
    }
}
