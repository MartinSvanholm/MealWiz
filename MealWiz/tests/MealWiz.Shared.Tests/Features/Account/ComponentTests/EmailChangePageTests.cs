using Bunit;
using MealWiz.Shared.Features.Account;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Account.ComponentTests;

public class EmailChangePageTests : ComponentTestBase
{
    private const string ConfirmOther = "email-change#message=Confirmation+link+accepted.+Please+proceed+to+confirm+link+sent+to+the+other+email";
    private const string Done = "email-change#access_token=access&expires_in=3600&refresh_token=refresh&token_type=bearer&type=email_change";

    [Fact]
    public void The_first_link_asks_for_the_other_address()
    {
        Navigation.NavigateTo(ConfirmOther);
        var cut = Render<EmailChangePage>();

        Assert.Equal("Confirmed. Now click the link we sent to your other address.", cut.Find(TestId("email-change-confirm-other")).TextContent.Trim());
        Assert.Empty(cut.FindAll(TestId("email-change-done")));
    }

    [Fact]
    public void The_last_link_says_the_email_has_changed()
    {
        Navigation.NavigateTo(Done);
        var cut = Render<EmailChangePage>();

        Assert.Equal("Your email has changed.", cut.Find(TestId("email-change-done")).TextContent.Trim());
    }

    [Theory]
    [InlineData("email-change")]
    [InlineData("email-change#error=access_denied&error_code=otp_expired&error_description=Email+link+is+invalid+or+has+expired")]
    [InlineData("email-change#type=signup&access_token=x")]
    public void Anything_else_is_an_invalid_link(string url)
    {
        Navigation.NavigateTo(url);
        var cut = Render<EmailChangePage>();

        Assert.Contains("invalid or has expired", cut.Find(TestId("email-change-invalid")).TextContent);
    }

    [Fact]
    public void Every_state_links_to_the_account_page()
    {
        Navigation.NavigateTo(Done);
        var cut = Render<EmailChangePage>();

        Assert.Equal("account", cut.Find(TestId("email-change-account-link")).GetAttribute("href"));
    }

    [Fact]
    public void The_link_is_removed_from_the_address_bar()
    {
        Navigation.NavigateTo(Done);
        Render<EmailChangePage>();

        Assert.Equal("email-change", CurrentPath);
        Assert.True(Navigation.History.First().Options.ReplaceHistoryEntry);
    }

    [Fact]
    public void Done_refreshes_the_session_only_when_signed_in()
    {
        Auth.SetAuthorized("user-id");
        Navigation.NavigateTo(Done);
        Render<EmailChangePage>();

        Assert.Equal(1, Keeper.RefreshNowCalls);
    }

    [Fact]
    public void Done_signed_out_does_not_touch_the_session()
    {
        Navigation.NavigateTo(Done);
        Render<EmailChangePage>();

        Assert.Equal(0, Keeper.RefreshNowCalls);
    }

    [Fact]
    public void The_first_link_does_not_refresh_the_session()
    {
        Auth.SetAuthorized("user-id");
        Navigation.NavigateTo(ConfirmOther);
        Render<EmailChangePage>();

        Assert.Equal(0, Keeper.RefreshNowCalls);
    }
}
