using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Account.DeleteAccount;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Account.ComponentTests;

public class DeleteAccountPanelTests : ComponentTestBase
{
    [Fact]
    public void The_panel_is_closed_until_the_button_is_clicked()
    {
        var cut = Render<DeleteAccountPanel>();

        Assert.Empty(cut.FindAll(TestId("delete-account-panel")));
        cut.Find(TestId("delete-account-open")).Click();

        Assert.NotNull(cut.Find(TestId("delete-account-panel")));
        Assert.Contains("can't be undone", cut.Markup);
    }

    [Fact]
    public void Cancel_closes_the_panel_and_clears_the_password()
    {
        var cut = Render<DeleteAccountPanel>();
        cut.Find(TestId("delete-account-open")).Click();
        cut.Find(InputOf("delete-account-password")).Input("secret");

        cut.Find(TestId("delete-account-cancel")).Click();

        Assert.Empty(cut.FindAll(TestId("delete-account-panel")));
        cut.Find(TestId("delete-account-open")).Click();
        Assert.Equal("", cut.Find(InputOf("delete-account-password")).GetAttribute("value") ?? "");
    }

    [Fact]
    public void A_failure_shows_its_message_in_the_panel_and_stays_on_the_page()
    {
        SetupSend<DeleteAccount.Command, Result>(Result.Fail("That password isn't right."));
        var cut = Render<DeleteAccountPanel>();
        var historyBefore = Navigation.History.Count;
        cut.Find(TestId("delete-account-open")).Click();
        cut.Find(InputOf("delete-account-password")).Input("wrong");

        cut.Find(TestId("delete-account-confirm")).Click();

        cut.WaitForAssertion(() => Assert.Equal("That password isn't right.", cut.Find(TestId("delete-account-error")).TextContent.Trim()));
        Assert.Equal("wrong", Assert.Single(Sent<DeleteAccount.Command>()).Password);
        Assert.Equal(historyBefore, Navigation.History.Count);
    }

    [Fact]
    public void Success_reloads_to_login()
    {
        SetupSend<DeleteAccount.Command, Result>(Result.Ok());
        var cut = Render<DeleteAccountPanel>();
        cut.Find(TestId("delete-account-open")).Click();
        cut.Find(InputOf("delete-account-password")).Input("right");

        cut.Find(TestId("delete-account-confirm")).Click();

        cut.WaitForAssertion(() =>
        {
            var navigation = Navigation.History.First();
            Assert.EndsWith("/login", navigation.Uri);
            Assert.True(navigation.Options.ForceLoad);
        });
    }

    [Fact]
    public void An_empty_password_sends_nothing()
    {
        var cut = Render<DeleteAccountPanel>();
        cut.Find(TestId("delete-account-open")).Click();

        cut.Find(TestId("delete-account-confirm")).Click();

        Assert.Empty(Sent<DeleteAccount.Command>());
    }
}
