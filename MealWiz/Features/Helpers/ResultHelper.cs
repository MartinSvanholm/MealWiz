using FluentResults;
using MealWizFeatures.Models.Supabase;
using MudBlazor;
using Supabase.Gotrue.Exceptions;

namespace MealWizFeatures.Helpers;

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
                    SupabaseExceptionResponse response = gotrueException.ConvertSupbaseException();
                    return new Error(response.msg).CausedBy(gotrueException);
                }

                return new Error(exception.Message).CausedBy(exception);
            };
        });
    }

    public static void Handle<T>(this Result<T> result, ISnackbar snackbar)
    {
        if (result.IsSuccess)
        {
            var success = result.Successes.FirstOrDefault();
            if (success == null) return;

            snackbar.Add(success.Message, Severity.Success);
        }
        else
        {
            snackbar.Add(result.Errors.First().Message, Severity.Error);
        }
    }
}