using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Authentication.ForgotPassword;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Authentication.ComponentTests;

public class ForgotPasswordPageTests : ComponentTestBase
{
    public ForgotPasswordPageTests()
    {
        Navigation.NavigateTo("/forgot-password");
    }

    [Fact]
    public void Renders_the_email_field_with_its_label_above_it()
    {
        var cut = Render<ForgotPasswordPage>();

        Assert.Equal("MealWiz", cut.Find(TestId("forgot-title")).TextContent.Trim());
        Assert.Equal("email", cut.Find(InputOf("forgot-email")).GetAttribute("type"));
        Assert.Equal(cut.Find(InputOf("forgot-email")).Id, cut.Find(TestId("forgot-email-label")).GetAttribute("for"));
        Assert.Equal("Send reset link", cut.Find(TestId("forgot-submit")).TextContent.Trim());
    }

    [Fact]
    public void Sending_asks_for_a_link_back_to_the_reset_page_and_says_to_check_the_email()
    {
        SetupSend<RequestPasswordReset.Command, Result>(Result.Ok());
        var cut = Render<ForgotPasswordPage>();

        cut.Find(InputOf("forgot-email")).Input("me@example.com");
        cut.Find(TestId("forgot-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("me@example.com", cut.Find(TestId("forgot-check-email")).TextContent));
        var command = Assert.Single(Sent<RequestPasswordReset.Command>());
        Assert.Equal(("me@example.com", "http://localhost/reset-password"), (command.Email, command.RedirectTo));
        Assert.Empty(cut.FindAll(TestId("forgot-submit")));
    }

    [Fact]
    public void Enter_in_the_email_sends_the_link()
    {
        SetupSend<RequestPasswordReset.Command, Result>(Result.Ok());
        var cut = Render<ForgotPasswordPage>();

        cut.Find(InputOf("forgot-email")).Input("me@example.com");
        PressEnter(cut.Find(InputOf("forgot-email")));

        cut.WaitForAssertion(() => Assert.Single(Sent<RequestPasswordReset.Command>()));
    }

    [Fact]
    public void An_empty_email_is_not_sent()
    {
        var cut = Render<ForgotPasswordPage>();

        cut.Find(TestId("forgot-submit")).Click();

        Assert.Empty(Sent<RequestPasswordReset.Command>());
        Assert.Empty(cut.FindAll(TestId("forgot-check-email")));
    }

    [Fact]
    public void A_failed_request_shows_the_error_and_stays_on_the_form()
    {
        SetupSend<RequestPasswordReset.Command, Result>(Result.Fail("Email rate limit exceeded"));
        var cut = Render<ForgotPasswordPage>();

        cut.Find(InputOf("forgot-email")).Input("me@example.com");
        cut.Find(TestId("forgot-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Email rate limit exceeded", SnackbarMessages));
        Assert.Empty(cut.FindAll(TestId("forgot-check-email")));
        Assert.False(cut.Find(TestId("forgot-submit")).HasAttribute("disabled"));
    }

    [Fact]
    public void Back_to_log_in_links_to_login()
    {
        var cut = Render<ForgotPasswordPage>();

        Assert.Equal("login", cut.Find(TestId("forgot-login")).GetAttribute("href"));
    }
}
