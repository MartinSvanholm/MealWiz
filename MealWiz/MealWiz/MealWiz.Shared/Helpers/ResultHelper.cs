using FluentResults;
using MudBlazor;
using Supabase.Gotrue.Exceptions;
using Supabase.Postgrest.Exceptions;

namespace MealWiz.Shared.Helpers;

public static class ResultHelper
{
    public static void SetDefaultCatchHandler()
    {
        Result.Setup(cfg =>
        {
            cfg.DefaultTryCatchHandler = exception =>
            {
                if (exception is GotrueException gotrueException)
                {
                    string errorMessage = "No response from server";
#if DEBUG    
                    errorMessage = SupabaseHelper.GetSupabaseErrorMessage(gotrueException).Message;
#endif
                    return new Error(errorMessage).CausedBy(gotrueException);
                }

                if (exception is PostgrestException postgrestException)
                {
                    string errorMessage = "No response from server";
#if DEBUG
                    errorMessage = SupabaseHelper.GetSupabaseErrorMessage(postgrestException).Message;
#endif
                    return new Error(errorMessage).CausedBy(postgrestException);
                }

                return new Error(exception.Message).CausedBy(exception);
            };
        });
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