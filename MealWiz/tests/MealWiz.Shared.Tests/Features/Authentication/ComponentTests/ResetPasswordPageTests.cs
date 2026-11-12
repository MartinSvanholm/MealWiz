using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Authentication.ResetPassword;
using MealWiz.Shared.Tests.TestInfrastructure;
using Supabase.Gotrue;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Authentication.ComponentTests;

public class ResetPasswordPageTests : ComponentTestBase
{
    private const string EmailLink = "reset-password#access_token=access&expires_in=3600&refresh_token=refresh&token_type=bearer&type=recovery";

    [Fact]
    public void A_link_from_the_email_asks_for_the_new_password_twice()
    {
        Navigation.NavigateTo(EmailLink);
        var cut = Render<ResetPasswordPage>();

        Assert.Equal("MealWiz", cut.Find(TestId("reset-title")).TextContent.Trim());
        Assert.Equal("password", cut.Find(InputOf("reset-password")).GetAttribute("type"));
        Assert.Equal("password", cut.Find(InputOf("reset-password-repeat")).GetAttribute("type"));
        Assert.Equal(cut.Find(InputOf("reset-password")).Id, cut.Find(TestId("reset-password-label")).GetAttribute("for"));
        Assert.Equal(cut.Find(InputOf("reset-password-repeat")).Id, cut.Find(TestId("reset-password-repeat-label")).GetAttribute("for"));
        Assert.Equal("Set password", cut.Find(TestId("reset-submit")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("reset-expired")));
    }

    // The fragment holds the recovery session's tokens: keep them out of the address bar and the history.
    [Fact]
    public void The_tokens_are_removed_from_the_address_bar()
    {
        Navigation.NavigateTo(EmailLink);
        Render<ResetPasswordPage>();

        Assert.Equal("reset-password", CurrentPath);
        Assert.True(Navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public void Matching_passwords_set_the_new_password_from_the_link_and_go_to_the_meal_plan()
    {
        SetupSend<ResetPassword.Command, Result<Session?>>(Result.Ok<Session?>(new Session()));
        Navigation.NavigateTo(EmailLink);
        var cut = Render<ResetPasswordPage>();

        Fill(cut, "newsecret", "newsecret");
        cut.Find(TestId("reset-submit")).Click();

        cut.WaitForAssertion(() => Assert.Equal("", CurrentPath));
        var command = Assert.Single(Sent<ResetPassword.Command>());
        Assert.Equal(Navigation.ToAbsoluteUri(EmailLink), command.ResetLink);
        Assert.Equal("newsecret", command.NewPassword);
    }

    [Fact]
    public void Enter_in_the_repeat_field_sets_the_password()
    {
        SetupSend<ResetPassword.Command, Result<Session?>>(Result.Ok<Session?>(new Session()));
        Navigation.NavigateTo(EmailLink);
        var cut = Render<ResetPasswordPage>();

        cut.Find(InputOf("reset-password")).Input("newsecret");
        PressEnter(cut.Find(InputOf("reset-password")));
        AssertFocused(cut.Find(InputOf("reset-password-repeat")));
        cut.Find(InputOf("reset-password-repeat")).Input("newsecret");
        PressEnter(cut.Find(InputOf("reset-password-repeat")));

        cut.WaitForAssertion(() => Assert.Single(Sent<ResetPassword.Command>()));
    }

    [Fact]
    public void Passwords_that_do_not_match_show_an_error_and_are_not_sent()
    {
        Navigation.NavigateTo(EmailLink);
        var cut = Render<ResetPasswordPage>();

        Fill(cut, "newsecret", "other");
        cut.Find(TestId("reset-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Passwords don't match", SnackbarMessages));
        Assert.Empty(Sent<ResetPassword.Command>());
    }

    [Fact]
    public void A_failed_reset_shows_the_error_and_stays()
    {
        SetupSend<ResetPassword.Command, Result<Session?>>(Result.Fail<Session?>("Password should be at least 6 characters."));
        Navigation.NavigateTo(EmailLink);
        var cut = Render<ResetPasswordPage>();

        Fill(cut, "short", "short");
        cut.Find(TestId("reset-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Password should be at least 6 characters.", SnackbarMessages));
        Assert.Equal("reset-password", CurrentPath);
        Assert.False(cut.Find(TestId("reset-submit")).HasAttribute("disabled"));
    }

    // What GoTrue redirects to when the link has expired or was used already.
    [Fact]
    public void An_expired_link_says_so_and_offers_a_new_one()
    {
        Navigation.NavigateTo("reset-password#error=access_denied&error_code=otp_expired&error_description=Email+link+is+invalid+or+has+expired");
        var cut = Render<ResetPasswordPage>();

        // GoTrue's English reason isn't shown: every bad link gets the one localized message.
        Assert.Equal(InvalidLink, cut.Find(TestId("reset-expired")).TextContent.Trim());
        Assert.Equal("forgot-password", cut.Find(TestId("reset-request-again")).GetAttribute("href"));
        Assert.Empty(cut.FindAll(TestId("reset-submit")));
    }

    [Fact]
    public void Opening_the_page_without_a_link_offers_a_new_one()
    {
        Navigation.NavigateTo("reset-password");
        var cut = Render<ResetPasswordPage>();

        Assert.Equal(InvalidLink, cut.Find(TestId("reset-expired")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("reset-submit")));
    }

    private const string InvalidLink = "This reset link is invalid or has expired. Ask for a new one.";

    private static void Fill(IRenderedComponent<ResetPasswordPage> cut, string password, string repeat)
    {
        cut.Find(InputOf("reset-password")).Input(password);
        cut.Find(InputOf("reset-password-repeat")).Input(repeat);
    }
}
