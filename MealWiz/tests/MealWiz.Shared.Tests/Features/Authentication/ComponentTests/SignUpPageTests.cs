using Bunit;
using FluentResults;
using MealWiz.Shared.Features.Authentication.SignUp;
using MealWiz.Shared.Tests.TestInfrastructure;
using Supabase.Gotrue;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Authentication.ComponentTests;

public class SignUpPageTests : ComponentTestBase
{
    public SignUpPageTests()
    {
        Navigation.NavigateTo("/signup");
    }

    [Fact]
    public void Renders_email_password_and_repeat_fields_with_labels_above_them()
    {
        var cut = Render<SignUpPage>();

        Assert.Equal("MealWiz", cut.Find(TestId("signup-title")).TextContent.Trim());
        Assert.Equal("email", cut.Find(InputOf("signup-email")).GetAttribute("type"));
        Assert.Equal("password", cut.Find(InputOf("signup-password")).GetAttribute("type"));
        Assert.Equal("password", cut.Find(InputOf("signup-password-repeat")).GetAttribute("type"));
        Assert.Equal(cut.Find(InputOf("signup-email")).Id, cut.Find(TestId("signup-email-label")).GetAttribute("for"));
        Assert.Equal(cut.Find(InputOf("signup-password")).Id, cut.Find(TestId("signup-password-label")).GetAttribute("for"));
        Assert.Equal(cut.Find(InputOf("signup-password-repeat")).Id, cut.Find(TestId("signup-password-repeat-label")).GetAttribute("for"));
        Assert.Equal("Create account", cut.Find(TestId("signup-submit")).TextContent.Trim());
    }

    [Fact]
    public void Valid_input_creates_the_account_and_goes_to_the_meal_plan()
    {
        SetupSend<SignUp.Command, Result<Session?>>(Result.Ok<Session?>(new Session()));
        var cut = Render<SignUpPage>();

        Fill(cut, "me@example.com", "secret1", "secret1");
        cut.Find(TestId("signup-submit")).Click();

        cut.WaitForAssertion(() => Assert.Equal("", CurrentPath));
        var command = Assert.Single(Sent<SignUp.Command>());
        Assert.Equal(("me@example.com", "secret1"), (command.Email, command.Password));
    }

    [Fact]
    public void Passwords_that_do_not_match_show_an_error_and_are_not_sent()
    {
        var cut = Render<SignUpPage>();

        Fill(cut, "me@example.com", "secret1", "secret2");
        cut.Find(TestId("signup-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("Passwords don't match", SnackbarMessages));
        Assert.Empty(Sent<SignUp.Command>());
        Assert.Equal("signup", CurrentPath);
    }

    [Fact]
    public void Empty_fields_are_not_sent()
    {
        var cut = Render<SignUpPage>();

        cut.Find(TestId("signup-submit")).Click();

        Assert.Empty(Sent<SignUp.Command>());
        Assert.Equal("signup", CurrentPath);
    }

    [Fact]
    public void A_failed_sign_up_shows_the_error_and_stays()
    {
        SetupSend<SignUp.Command, Result<Session?>>(Result.Fail<Session?>("User already registered"));
        var cut = Render<SignUpPage>();

        Fill(cut, "me@example.com", "secret1", "secret1");
        cut.Find(TestId("signup-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("User already registered", SnackbarMessages));
        Assert.Equal("signup", CurrentPath);
        Assert.False(cut.Find(TestId("signup-submit")).HasAttribute("disabled"));
    }

    // A Supabase project that requires email confirmation creates the user but returns no session.
    [Fact]
    public void Without_a_session_it_asks_the_user_to_confirm_their_email()
    {
        SetupSend<SignUp.Command, Result<Session?>>(Result.Ok<Session?>(null));
        var cut = Render<SignUpPage>();

        Fill(cut, "me@example.com", "secret1", "secret1");
        cut.Find(TestId("signup-submit")).Click();

        cut.WaitForAssertion(() => Assert.Contains("me@example.com", cut.Find(TestId("signup-check-email")).TextContent));
        Assert.Empty(cut.FindAll(TestId("signup-submit")));
        Assert.Equal("signup", CurrentPath);
    }

    [Fact]
    public void Enter_moves_through_the_fields_and_creates_the_account_from_the_last_one()
    {
        SetupSend<SignUp.Command, Result<Session?>>(Result.Ok<Session?>(new Session()));
        var cut = Render<SignUpPage>();

        // Type into each field only after checking it got focus: typing re-renders it and drops bUnit's element reference.
        cut.Find(InputOf("signup-email")).Input("me@example.com");
        PressEnter(cut.Find(InputOf("signup-email")));
        AssertFocused(cut.Find(InputOf("signup-password")));
        cut.Find(InputOf("signup-password")).Input("secret1");
        PressEnter(cut.Find(InputOf("signup-password")));
        AssertFocused(cut.Find(InputOf("signup-password-repeat")));
        Assert.Empty(Sent<SignUp.Command>());

        cut.Find(InputOf("signup-password-repeat")).Input("secret1");
        PressEnter(cut.Find(InputOf("signup-password-repeat")));

        cut.WaitForAssertion(() => Assert.Equal("", CurrentPath));
        Assert.Single(Sent<SignUp.Command>());
    }

    [Fact]
    public void Log_in_link_goes_to_login()
    {
        var cut = Render<SignUpPage>();

        Assert.Equal("login", cut.Find(TestId("signup-login")).GetAttribute("href"));
    }

    private static void Fill(IRenderedComponent<SignUpPage> cut, string email, string password, string repeat)
    {
        cut.Find(InputOf("signup-email")).Input(email);
        cut.Find(InputOf("signup-password")).Input(password);
        cut.Find(InputOf("signup-password-repeat")).Input(repeat);
    }
}
