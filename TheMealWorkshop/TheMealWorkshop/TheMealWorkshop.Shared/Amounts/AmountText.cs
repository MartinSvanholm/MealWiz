using System.Globalization;
using TheMealWorkshop.Shared.Localization;

namespace TheMealWorkshop.Shared.Amounts;

/// <summary>Amounts as the UI shows them, in the current culture (UI-03): decimal comma in Danish, short unit names.</summary>
public static class AmountText
{
    /// <summary>"1,5 dl" / "1.5 dl" / "2": the quantity (at most 2 decimals, no grouping) and the unit's short name.</summary>
    public static string Format(decimal quantity, AmountUnit? unit)
    {
        var number = Number(quantity);
        return unit is null ? number : $"{number} {SharedTexts.Get($"Unit_{unit.Code}_Short")}";
    }

    /// <summary>
    /// The helper text under an amount field: "200 · gram" when understood, "Saved as written" when not, null when
    /// the field is empty.
    /// </summary>
    public static string? Describe(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var parsed = ParsedAmount.Parse(text);
        if (!parsed.IsUnderstood) return SharedTexts.Get("Amount_SavedAsWritten");

        var name = SharedTexts.Get(parsed.Unit is null ? "Unit_Count_Name" : $"Unit_{parsed.Unit.Code}_Name");
        return string.Format(CultureInfo.CurrentCulture, SharedTexts.Get("Amount_Understood"), Number(parsed.Quantity!.Value), name);
    }

    private static string Number(decimal quantity) => quantity.ToString("0.##", CultureInfo.CurrentCulture);
}
