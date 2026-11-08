using FluentResults;
using MudBlazor;
using Supabase.Gotrue.Exceptions;
using Supabase.Postgrest.Exceptions;

namespace MealWiz.Shared.Helpers;

public static class ResultHelper
{
    public static void SetDefaultCatchHandler()
    {
        Result.Setup(cfg => cfg.DefaultTryCatchHandler = MapException);
    }

    /// <summary>
    /// The default <c>Result.Try</c> catch handler: turns a caught exception into an error whose message, in the
    /// current UI culture, is what the user sees. Debug builds show Supabase's own message instead, for developers.
    /// </summary>
    internal static IError MapException(Exception exception)
    {
        if (exception is GotrueException gotrueException)
        {
            string errorMessage = ErrorMessages.NoResponse;
#if DEBUG
            errorMessage = SupabaseHelper.GetSupabaseErrorMessage(gotrueException).Message;
#endif
            return new Error(errorMessage).CausedBy(gotrueException);
        }

        if (exception is PostgrestException postgrestException)
        {
            // A unique violation (23505): another device got there first. Same message in Debug and Release.
            if (SupabaseHelper.GetSupabaseErrorMessage(postgrestException).Code == "23505")
                return new Error(ErrorMessages.ChangedOnAnotherDevice).CausedBy(postgrestException);

            string errorMessage = ErrorMessages.NoResponse;
#if DEBUG
            errorMessage = SupabaseHelper.GetSupabaseErrorMessage(postgrestException).Message;
#endif
            return new Error(errorMessage).CausedBy(postgrestException);
        }

        // Raw exception text isn't for users (it's English and technical); it stays on the cause for diagnosis.
        return new Error(ErrorMessages.GenericError).CausedBy(exception);
    }

    public static Result<T> Handle<T>(this Result<T> result, ISnackbar snackbar)
    {
        if (result.IsSuccess)
        {
            var success = result.Successes.FirstOrDefault();
            if (success == null) return result;

            snackbar.Add(success.Message, Severity.Success);
        }
        else
        {
            snackbar.Add(result.Errors.First().Message, Severity.Error);
        }

        return result;
    }

    public static Result Handle(this Result result, ISnackbar snackbar)
    {
        if (result.IsSuccess)
        {
            var success = result.Successes.FirstOrDefault();
            if (success == null) return result;

            snackbar.Add(success.Message, Severity.Success);
        }
        else
        {
            snackbar.Add(result.Errors.First().Message, Severity.Error);
        }

        return result;
    }
}