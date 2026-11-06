using Microsoft.Extensions.Localization;
using MudBlazor;

namespace MealWiz.Shared.Localization;

/// <summary>
/// Translates the few MudBlazor built-in texts this app shows (screen-reader labels) from the shared resource.
/// MudBlazor asks it only when the UI culture isn't English; any other key returns "not found", so MudBlazor falls
/// back to its own English text. Add a key here when the app starts showing another built-in MudBlazor text.
/// </summary>
public sealed class MealWizMudLocalizer(IStringLocalizer<SharedResource> localizer) : MudLocalizer
{
    private static readonly Dictionary<string, string> SharedKeys = new()
    {
        ["MudInput_Clear"] = "Common_Clear",        // the clear button on Clearable search fields
        ["MudSnackbar_Close"] = "Common_Close",     // the close button on snackbars
    };

    public override LocalizedString this[string key] =>
        SharedKeys.TryGetValue(key, out var sharedKey)
            ? new LocalizedString(key, localizer[sharedKey].Value)
            : new LocalizedString(key, key, resourceNotFound: true);

    public override LocalizedString this[string key, params object[] arguments] =>
        SharedKeys.ContainsKey(key)
            ? new LocalizedString(key, string.Format(this[key].Value, arguments))
            : new LocalizedString(key, key, resourceNotFound: true);
}
