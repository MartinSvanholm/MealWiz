using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Account.ChangeEmail;
using MealWiz.Shared.Features.Account.GetPendingEmailChange;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Account.ComponentTests;

public class ChangeEmailFormTests : ComponentTestBase
{
    [Fact]
    public void Without_a_pending_change_there_is_no_note()
    {
        var cut = Render<ChangeEmailForm>();

        Assert.Empty(cut.FindAll(TestId("account-email-pending")));
    }

    [Fact]
    public void A_pending_change_is_shown_on_load()
    {
        SetupSend<GetPendingEmailChange.Query, Result<string?>>(Result.Ok<string?>("new@example.com"));
        var cut = Render<ChangeEmailForm>();

        cut.WaitForAssertion(() => Assert.Equal(
            "Waiting for confirmation of new@example.com. Click the links we sent to both addresses.",
            cut.Find(TestId("account-email-pending")).TextContent.Trim()));
    }

    [Fact]
    public void Asking_for_a_change_sends_it_and_shows_the_pending_note()
    {
        SetupSend<ChangeEmail.Command, Result>(Result.Ok());
        var cut = Render<ChangeEmailForm>();

        cut.Find(InputOf("change-email-new")).Input("new@example.com");
        cut.Find(TestId("change-email-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("new@example.com", cut.Find(TestId("account-email-pending")).TextContent));
        Assert.Equal("new@example.com", Assert.Single(Sent<ChangeEmail.Command>()).NewEmail);
    }

    [Fact]
    public void A_failure_shows_its_message_and_no_pending_note()
    {
        SetupSend<ChangeEmail.Command, Result>(Result.Fail("That email already belongs to another account."));
        var cut = Render<ChangeEmailForm>();

        cut.Find(InputOf("change-email-new")).Input("taken@example.com");
        cut.Find(TestId("change-email-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("That email already belongs to another account.", SnackbarMessages));
        Assert.Empty(cut.FindAll(TestId("account-email-pending")));
    }

    [Fact]
    public void An_empty_email_is_not_sent()
    {
        var cut = Render<ChangeEmailForm>();

        cut.Find(TestId("change-email-submit")).Click();

        Assert.Empty(Sent<ChangeEmail.Command>());
    }
}
