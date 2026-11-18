using FluentResults;
using TheMealWorkshop.Shared.Helpers;
using Microsoft.Extensions.Localization;
using Supabase.Postgrest.Exceptions;

namespace TheMealWorkshop.Shared.Features.GroceryCategories.Models;

/// <summary>Turns the categories' database errors into the user's messages; anything else goes to <see cref="ResultHelper"/>.</summary>
public static class GroceryCategoryErrors
{
    public static IError Map(Exception exception, IStringLocalizer<GroceryCategoriesResource> localizer)
    {
        var message = exception.Message;
        string? key =
            exception is PostgrestException && SupabaseHelper.GetSupabaseErrorMessage(exception).Code == "23505" ? "GroceryCategory_NameTaken"
            : message.Contains("grocery_category_is_other") ? "GroceryCategory_OtherCannotBeDeleted"
            : message.Contains("grocery_category_same") ? "GroceryCategory_MoveToItself"
            : message.Contains("grocery_category_not_found") ? "GroceryCategory_NotFound"
            : null;

        return key is null ? ResultHelper.MapException(exception) : new Error(localizer[key]).CausedBy(exception);
    }
}
