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

    public static SupabaseExceptionMessage GetSupabaseErrorMessage(Exception exception)
    {
        return JsonConvert.DeserializeObject<SupabaseExceptionMessage>(exception.Message);
    }
}