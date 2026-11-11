using System.Security.Claims;
using Bunit;
using MealWiz.Shared.Features.Account;
using MealWiz.Shared.Tests.TestInfrastructure;
using MealWiz.Shared.Localization;
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

    [Fact]
    public void The_language_dropdown_lists_device_default_then_each_language_in_its_own_name()
    {
        var cut = Render<AccountPage>();

        var options = cut.Find(TestId("language-select")).QuerySelectorAll("option");

        Assert.Equal(["", "en", "da"], options.Select(o => o.GetAttribute("value")));
        Assert.Equal(["Device default", "English", "Dansk"], options.Select(o => o.TextContent.Trim()));
    }

    [Fact]
    public void The_language_dropdown_preselects_device_default_when_nothing_is_stored()
    {
        Language.Setup(l => l.GetAsync()).ReturnsAsync((string?)null);

        var cut = Render<AccountPage>();

        cut.WaitForAssertion(() => Assert.Equal("", cut.Find(TestId("language-select")).GetAttribute("value")));
    }

    [Fact]
    public void The_language_dropdown_preselects_the_stored_language()
    {
        Language.Setup(l => l.GetAsync()).ReturnsAsync("da");

        var cut = Render<AccountPage>();

        cut.WaitForAssertion(() => Assert.Equal("da", cut.Find(TestId("language-select")).GetAttribute("value")));
    }

    [Fact]
    public void Picking_a_language_saves_it_through_the_language_preference()
    {
        var cut = Render<AccountPage>();

        cut.Find(TestId("language-select")).Change("da");

        Language.Verify(l => l.SetAsync("da"), Times.Once);
    }

    [Fact]
    public void Picking_device_default_clears_the_stored_choice()
    {
        Language.Setup(l => l.GetAsync()).ReturnsAsync("da");
        var cut = Render<AccountPage>();

        cut.Find(TestId("language-select")).Change("");

        Language.Verify(l => l.SetAsync(null), Times.Once);
    }

    [Fact]
    public void Picking_the_language_that_is_already_chosen_does_not_reload()
    {
        Language.Setup(l => l.GetAsync()).ReturnsAsync("da");
        var cut = Render<AccountPage>();
        cut.WaitForAssertion(() => Assert.Equal("da", cut.Find(TestId("language-select")).GetAttribute("value")));

        cut.Find(TestId("language-select")).Change("da");

        Language.Verify(l => l.SetAsync(It.IsAny<string?>()), Times.Never);
    }
}
