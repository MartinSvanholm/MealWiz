using MealWizFeatures.Models.Supabase;
using Newtonsoft.Json;
using Supabase.Gotrue.Exceptions;

namespace MealWizFeatures.Helpers;

public static class SupabaseHelper
{
    public static SupabaseExceptionResponse ConvertSupbaseException(this GotrueException gotrueException)
    {
        return JsonConvert.DeserializeObject<SupabaseExceptionResponse>(gotrueException.Message);
    }
}