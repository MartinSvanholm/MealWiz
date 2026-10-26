using Bunit;
using MealWiz.Shared.Components.Form;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.AspNetCore.Components.Web;
using Xunit;

namespace MealWiz.Shared.Tests.Components.ComponentTests;

public class FormTests : ComponentTestBase
{
    [Fact]
    public void TextField_renders_its_label_and_value()
    {
        var cut = Render<TextField>(p => p
            .Add(t => t.Label, "Email")
            .Add(t => t.Value, "me@example.com"));

        Assert.Contains("Email", cut.Markup);
        Assert.Equal("me@example.com", cut.Find("input").GetAttribute("value"));
    }

    [Fact]
    public void Typing_in_TextField_raises_ValueChanged()
    {
        string? value = null;
        var cut = Render<TextField>(p => p
            .Add(t => t.Label, "Email")
            .Add(t => t.ValueChanged, (string v) => value = v));

        cut.Find("input").Input("me@example.com");

        Assert.Equal("me@example.com", value);
    }

    [Fact]
    public void Pressing_Enter_in_CustomForm_invokes_OnEnterClicked()
    {
        var entered = 0;
        var cut = Render<CustomForm>(p => p
            .Add(f => f.OnEnterClicked, () => entered++)
            .AddChildContent<TextField>(t => t.Add(f => f.Label, "Email")));

        cut.Find("form").KeyPress(new KeyboardEventArgs { Key = "a" });
        cut.Find("form").KeyPress(new KeyboardEventArgs { Key = "Enter" });

        Assert.Equal(1, entered);
    }

    [Fact]
    public async Task Validate_with_an_empty_required_field_reports_invalid()
    {
        bool? isValid = null;
        var cut = Render<CustomForm>(p => p
            .Add(f => f.IsValid, true)
            .Add(f => f.IsValidChanged, (bool v) => isValid = v)
            .AddChildContent<TextField>(t => t.Add(f => f.Label, "Email").Add(f => f.Required, true)));

        await cut.InvokeAsync(() => cut.Instance.Validate());

        Assert.False(isValid);
    }
}
