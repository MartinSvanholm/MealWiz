using System.Security.Claims;
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
    public void Shows_who_is_signed_in_from_the_email_claim()
    {
        // Supabase's access token carries the address in an "email" claim; the identity has no name claim.
        Auth.SetAuthorized("4f1c2b7e-user-id");
        Auth.SetClaims(new Claim("email", "martin@example.com"));

        var cut = Render<AccountPage>();

        Assert.Equal("martin@example.com", cut.Find(TestId("account-email")).TextContent.Trim());
        Assert.Equal("M", cut.Find(TestId("account-avatar")).TextContent.Trim());
    }

    [Fact]
    public void Toggling_dark_mode_goes_through_the_theme_service()
    {
        var cut = Render<AccountPage>();

        cut.Find(InputOf("dark-mode-switch")).Change(true);

        Theme.Verify(t => t.ToggleAsync(), Times.Once);
    }

    [Fact]
    public void The_dark_mode_switch_follows_the_theme_once_it_has_loaded()
    {
        // MEA-71: on a direct load of /account the page renders before ThemeService has read the stored theme,
        // so the switch must update when the theme service raises OnChange.
        Theme.SetupGet(t => t.IsDarkMode).Returns(false);
        var cut = Render<AccountPage>();
        Assert.False(cut.Find(InputOf("dark-mode-switch")).HasAttribute("checked"));

        Theme.SetupGet(t => t.IsDarkMode).Returns(true);
        cut.InvokeAsync(() => Theme.Raise(t => t.OnChange += null));

        cut.WaitForAssertion(() => Assert.True(cut.Find(InputOf("dark-mode-switch")).HasAttribute("checked")));
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
