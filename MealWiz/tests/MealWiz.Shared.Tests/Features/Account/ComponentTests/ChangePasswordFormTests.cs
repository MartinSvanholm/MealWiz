using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Account.ChangePassword;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Account.ComponentTests;

public class ChangePasswordFormTests : ComponentTestBase
{
    private static void Fill(IRenderedComponent<ChangePasswordForm> cut, string password, string repeat, Func<string, string> input)
    {
        cut.Find(input("change-password-new")).Input(password);
        cut.Find(input("change-password-repeat")).Input(repeat);
    }

    [Fact]
    public void A_mismatch_is_caught_on_the_page_and_nothing_is_sent()
    {
        var cut = Render<ChangePasswordForm>();

        Fill(cut, "secret1", "secret2", InputOf);
        cut.Find(TestId("change-password-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Passwords don't match", SnackbarMessages));
        Assert.Empty(Sent<ChangePassword.Command>());
    }

    [Fact]
    public void Success_shows_the_message_and_clears_the_fields()
    {
        SetupSend<ChangePassword.Command, Result>(Result.Ok().WithSuccess("Password changed"));
        var cut = Render<ChangePasswordForm>();

        Fill(cut, "secret1", "secret1", InputOf);
        cut.Find(TestId("change-password-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Password changed", SnackbarMessages));
        Assert.Equal("secret1", Assert.Single(Sent<ChangePassword.Command>()).NewPassword);
        // Cleared: pressing the button again has nothing to send (both fields are required).
        cut.Find(TestId("change-password-submit")).Click();
        cut.WaitForState(() => !cut.Find(TestId("change-password-submit")).HasAttribute("disabled"));
        Assert.Single(Sent<ChangePassword.Command>());
    }

    [Fact]
    public void A_failure_shows_its_message_and_keeps_the_fields()
    {
        SetupSend<ChangePassword.Command, Result>(Result.Fail("Use at least 6 characters."));
        var cut = Render<ChangePasswordForm>();

        Fill(cut, "abc", "abc", InputOf);
        cut.Find(TestId("change-password-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Use at least 6 characters.", SnackbarMessages));
        // Kept: pressing the button again sends the same password.
        cut.WaitForState(() => !cut.Find(TestId("change-password-submit")).HasAttribute("disabled"));
        cut.Find(TestId("change-password-submit")).Click();
        cut.WaitForAssertion(() => Assert.Equal(2, Sent<ChangePassword.Command>().Count));
    }
}
