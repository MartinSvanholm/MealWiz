using FluentResults;
using MealWiz.Shared.Helpers;
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
    public void MapException_OtherException_KeepsItsMessageAndCause()
    {
        var exception = new HttpRequestException("Connection refused");

        var error = ResultHelper.MapException(exception);

        Assert.Equal("Connection refused", error.Message);
        Assert.Same(exception, CauseOf(error));
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
