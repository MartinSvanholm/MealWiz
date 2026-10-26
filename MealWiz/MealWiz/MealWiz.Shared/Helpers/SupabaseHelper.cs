using MealWiz.Shared.Models.Supabase;
using Newtonsoft.Json;
using Supabase.Gotrue.Exceptions;
using System.Net;

namespace MealWiz.Shared.Helpers;

public static class SupabaseHelper
{
    public static SupabaseExceptionResponse ConvertGoTrueException(this GotrueException gotrueException)
    {
        if (gotrueException.Response == null)
        {
            return new SupabaseExceptionResponse
            {
                code = (int)HttpStatusCode.ServiceUnavailable,
                msg = "No response from server"
            };
        }

        return JsonConvert.DeserializeObject<SupabaseExceptionResponse>(gotrueException.Message);
    }

    /// <summary>
    /// Reads the JSON error body Supabase puts in the exception message. Falls back to the raw message when it isn't
    /// JSON or has no message (e.g. a network failure): this runs inside the Result.Try catch handler, so it must not throw.
    /// </summary>
    public static SupabaseExceptionMessage GetSupabaseErrorMessage(Exception exception)
    {
        SupabaseExceptionMessage? message;
        try
        {
            message = JsonConvert.DeserializeObject<SupabaseExceptionMessage>(exception.Message);
        }
        catch (JsonException)
        {
            message = null;
        }

        if (string.IsNullOrEmpty(message?.Message))
            return new SupabaseExceptionMessage { Code = message?.Code, Details = message?.Details, Hint = message?.Hint, Message = exception.Message };

        return message;
    }
}