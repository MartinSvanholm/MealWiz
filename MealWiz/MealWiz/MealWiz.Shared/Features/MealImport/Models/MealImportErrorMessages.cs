using Microsoft.Extensions.Localization;

namespace MealWiz.Shared.Features.MealImport.Models;

public static class MealImportErrorMessages
{
    /// <summary>
    /// What to tell the user about a failed import: the localized message for its code (<c>ImportError_&lt;Code&gt;</c>).
    /// No code, or one this app doesn't know, gets the generic <c>ImportError_Unknown</c>.
    /// </summary>
    public static string ToMessage(this IStringLocalizer<MealImportResource> localizer, MealImportErrorCode? code) =>
        localizer[$"ImportError_{code ?? MealImportErrorCode.Unknown}"].Value;
}
