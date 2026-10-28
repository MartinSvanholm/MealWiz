using Bunit;
using MealWiz.Shared.Features.Account;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Account.ComponentTests;

public class AccountPageTests : ComponentTestBase
{
    [Fact]
    public void Renders_the_settings_with_a_logout_button()
    {
        var cut = Render<AccountPage>();

        Assert.Contains("Dark mode", cut.Markup);
        Assert.NotNull(cut.Find(TestId("logout-button")));
    }

    [Fact]
    public void Toggling_dark_mode_goes_through_the_theme_service()
    {
        var cut = Render<AccountPage>();

        cut.Find(InputOf("dark-mode-switch")).Change(true);

        Theme.Verify(t => t.ToggleAsync(), Times.Once);
    }

    [Fact]
    public void Back_returns_to_the_meal_plan()
    {
        Navigation.NavigateTo("/account");
        var cut = Render<AccountPage>();

        cut.Find(TestId("account-back")).Click();

        Assert.Equal("", CurrentPath);
    }
}
