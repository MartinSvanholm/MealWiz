using MealWiz.Shared.Localization;

namespace MealWiz.Shared.Helpers;

/// <summary>Messages shared by several features, in the current UI culture (from <see cref="SharedResource"/>).</summary>
public static class ErrorMessages
{
    public static string GenericError => SharedTexts.Get("Error_Generic");
    public static string ChangedOnAnotherDevice => SharedTexts.Get("Error_ChangedOnAnotherDevice");
    public static string NotSignedIn => SharedTexts.Get("Error_NotSignedIn");
    public static string NoResponse => SharedTexts.Get("Error_NoResponse");
}
