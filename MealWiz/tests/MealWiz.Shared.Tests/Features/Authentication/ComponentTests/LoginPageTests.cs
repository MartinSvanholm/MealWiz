using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Authentication.Login;
using MealWiz.Shared.Features.Authentication.Logout;
using MealWiz.Shared.Tests.TestInfrastructure;
using Supabase.Gotrue;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Authentication.ComponentTests;

/// <summary>LoginPage and LogoutButton.</summary>
public class LoginPageTests : ComponentTestBase
{
    public LoginPageTests()
    {
        Navigation.NavigateTo("/login");
    }

    [Fact]
    public void Renders_email_and_password_fields()
    {
        var cut = Render<LoginPage>();

        Assert.Equal("email", cut.Find(InputOf("login-email")).GetAttribute("type"));
        Assert.Equal("password", cut.Find(InputOf("login-password")).GetAttribute("type"));
        Assert.NotNull(cut.Find(TestId("login-submit")));
    }

    [Fact]
    public void Valid_credentials_sign_in_and_go_to_the_meal_plan()
    {
        SetupSend<Login.Command, Result<Session?>>(Result.Ok<Session?>(new Session()));
        var cut = Render<LoginPage>();

        cut.Find(InputOf("login-email")).Input("me@example.com");
        cut.Find(InputOf("login-password")).Input("secret");
        cut.Find(TestId("login-submit")).Click();

        cut.WaitForAssertion(() => Assert.Equal("", CurrentPath));
        var command = Assert.Single(Sent<Login.Command>());
        Assert.Equal(("me@example.com", "secret"), (command.Email, command.Password));
    }

    [Fact]
    public void Wrong_credentials_show_the_error_and_stay_on_login()
    {
        SetupSend<Login.Command, Result<Session?>>(Result.Fail<Session?>("Invalid login credentials"));
        var cut = Render<LoginPage>();

        cut.Find(InputOf("login-email")).Input("me@example.com");
        cut.Find(InputOf("login-password")).Input("wrong");
        cut.Find(TestId("login-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Invalid login credentials", SnackbarMessages));
        Assert.Equal("login", CurrentPath);
    }

    [Fact]
    public void Empty_fields_are_not_sent()
    {
        var cut = Render<LoginPage>();

        cut.Find(TestId("login-submit")).Click();

        Assert.Empty(Sent<Login.Command>());
        Assert.Equal("login", CurrentPath);
    }

    [Fact]
    public void Logout_signs_out_and_reloads_the_page()
    {
        SetupSend<Logout.Command, Result>(Result.Ok());
        var historyBefore = Navigation.History.Count;
        var cut = Render<LogoutButton>();

        cut.Find(TestId("logout-button")).Click();

        Assert.Single(Sent<Logout.Command>());
        cut.WaitForAssertion(() => Assert.True(Navigation.History.First().Options.ForceLoad || Navigation.History.Count > historyBefore));
    }

    [Fact]
    public void A_failed_logout_shows_the_error()
    {
        SetupSend<Logout.Command, Result>(Result.Fail("Offline"));
        var historyBefore = Navigation.History.Count;
        var cut = Render<LogoutButton>();

        cut.Find(TestId("logout-button")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Offline", SnackbarMessages));
        Assert.Equal(historyBefore, Navigation.History.Count);
    }
}
