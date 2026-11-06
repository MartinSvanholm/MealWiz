using FluentResults;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Moq;
using MudBlazor;
using Supabase.Gotrue.Exceptions;
using Supabase.Postgrest.Exceptions;
using Xunit;

namespace MealWiz.Shared.Tests.Helpers.UnitTests;

/// <summary>
/// Tests <see cref="ResultHelper.MapException"/> directly rather than through <see cref="ResultHelper.SetDefaultCatchHandler"/>,
/// which would change FluentResults' global settings for every test running in parallel.
/// </summary>
public class ResultHelperTests
{
    private const string SupabaseJson = """{"code":"42501","message":"permission denied"}""";

    private readonly Mock<ISnackbar> _snackbar = new();

    [Fact]
    public void MapException_OtherException_GivesTheGenericErrorAndKeepsTheCause()
    {
        var exception = new HttpRequestException("Connection refused");

        var error = ResultHelper.MapException(exception);

        // The raw exception text is never shown to users; it stays on the cause for diagnosis.
        Assert.Equal("An error has occurred, please try again.", error.Message);
        Assert.Same(exception, CauseOf(error));
    }

    [Fact]
    public void MapException_OtherException_IsLocalized()
    {
        using var _ = CultureScope.Use("da");

        Assert.Equal("Der opstod en fejl. Prøv igen.", ResultHelper.MapException(new InvalidOperationException("boom")).Message);
    }

    [Fact]
    public void ErrorMessages_ComeFromTheSharedResource()
    {
        Assert.Equal("You need to be signed in to do that.", ErrorMessages.NotSignedIn);
        Assert.Equal("No response from server", ErrorMessages.NoResponse);

        using var _ = CultureScope.Use("da");
        Assert.Equal("Du skal være logget ind for at gøre det.", ErrorMessages.NotSignedIn);
        Assert.Equal("Der opstod en fejl. Prøv igen.", ErrorMessages.GenericError);
        Assert.Equal("Intet svar fra serveren", ErrorMessages.NoResponse);
        Assert.Equal("Det blev lige ændret på en anden enhed. Genindlæs for at se det.", ErrorMessages.ChangedOnAnotherDevice);
    }

    [Fact]
    public void MapException_GotrueException_UsesTheSupabaseMessage()
    {
        var exception = new GotrueException(SupabaseJson);

        var error = ResultHelper.MapException(exception);

        Assert.Equal(ExpectedSupabaseMessage("permission denied"), error.Message);
        Assert.Same(exception, CauseOf(error));
    }

    [Fact]
    public void MapException_PostgrestException_UsesTheSupabaseMessage()
    {
        var exception = new PostgrestException(SupabaseJson);

        var error = ResultHelper.MapException(exception);

        Assert.Equal(ExpectedSupabaseMessage("permission denied"), error.Message);
        Assert.Same(exception, CauseOf(error));
    }

    [Fact]
    public void MapException_UniqueViolation_SaysItChangedOnAnotherDevice()
    {
        var exception = new PostgrestException("""{"code":"23505","message":"duplicate key value violates unique constraint"}""");

        var error = ResultHelper.MapException(exception);

        Assert.Equal(ErrorMessages.ChangedOnAnotherDevice, error.Message);
        Assert.Equal("This was just changed on another device. Reload to see it.", error.Message);
        Assert.Same(exception, CauseOf(error));
    }

    [Fact]
    public void MapException_OtherPostgrestError_IsUnchanged()
    {
        var error = ResultHelper.MapException(new PostgrestException(SupabaseJson));

        Assert.NotEqual(ErrorMessages.ChangedOnAnotherDevice, error.Message);
    }

    [Fact]
    public void MapException_SupabaseExceptionWithNonJsonMessage_DoesNotThrow()
    {
        var postgrest = new PostgrestException("No such host is known.");
        var gotrue = new GotrueException("No such host is known.");

        Assert.Equal(ExpectedSupabaseMessage("No such host is known."), ResultHelper.MapException(postgrest).Message);
        Assert.Equal(ExpectedSupabaseMessage("No such host is known."), ResultHelper.MapException(gotrue).Message);
    }

    [Fact]
    public void Handle_SuccessWithMessage_ShowsASuccessToast()
    {
        var result = Result.Ok(5).WithSuccess("Saved");

        var returned = result.Handle(_snackbar.Object);

        Assert.Same(result, returned);
        VerifyToast("Saved", Severity.Success);
    }

    [Fact]
    public void Handle_PlainSuccess_ShowsNoToast()
    {
        Result.Ok(5).Handle(_snackbar.Object);
        Result.Ok().Handle(_snackbar.Object);

        VerifyNoToast();
    }

    [Fact]
    public void Handle_Failure_ShowsAnErrorToastWithTheFirstError()
    {
        var result = Result.Fail<int>("First").WithError("Second");

        var returned = result.Handle(_snackbar.Object);

        Assert.Same(result, returned);
        VerifyToast("First", Severity.Error);
        _snackbar.Verify(s => s.Add("Second", It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Handle_NonGenericSuccessWithMessage_ShowsASuccessToast()
    {
        var result = Result.Ok().WithSuccess("Deleted");

        var returned = result.Handle(_snackbar.Object);

        Assert.Same(result, returned);
        VerifyToast("Deleted", Severity.Success);
    }

    [Fact]
    public void Handle_NonGenericFailure_ShowsAnErrorToast()
    {
        var result = Result.Fail("Nope");

        var returned = result.Handle(_snackbar.Object);

        Assert.Same(result, returned);
        VerifyToast("Nope", Severity.Error);
    }

    // Supabase's own error text is only shown in Debug builds; Release hides it behind a generic message.
    private static string ExpectedSupabaseMessage(string debugMessage)
    {
#if DEBUG
        return debugMessage;
#else
        return "No response from server";
#endif
    }

    private static Exception CauseOf(IError error) =>
        Assert.IsType<ExceptionalError>(Assert.Single(error.Reasons)).Exception;

    private void VerifyToast(string message, Severity severity) =>
        _snackbar.Verify(s => s.Add(message, severity, It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Once);

    private void VerifyNoToast() =>
        _snackbar.Verify(s => s.Add(It.IsAny<string>(), It.IsAny<Severity>(), It.IsAny<Action<SnackbarOptions>>(), It.IsAny<string>()), Times.Never);
}
