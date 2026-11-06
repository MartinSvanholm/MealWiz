using System.Globalization;
using System.Text.RegularExpressions;

namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>
/// Adds up grocery amounts typed as text ("200 g", "1,5 dl"). Any single word of letters counts as a unit; units
/// are compared ignoring case and a trailing ".", and never converted. Anything it can't read (fractions, ranges,
/// thousands separators, more than two decimals) makes the suggestion null so the user types the total.
/// </summary>
public static partial class AmountHelper
{
    [GeneratedRegex(@"^(\d+(?:[.,]\d{1,2})?)\s*(\p{L}+\.?)?$", RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    public static bool TryParse(string? amount, out decimal quantity, out string unit)
    {
        var parsed = Parse(amount);
        quantity = parsed?.Quantity ?? 0;
        unit = parsed?.Unit.TrimEnd('.') ?? string.Empty;
        return parsed != null;
    }

    /// <summary>The summed amount in the first amount's unit and decimal separator, or null when it can't be added.</summary>
    public static string? SuggestTotal(IEnumerable<string?> amounts)
    {
        var parsed = new List<Parsed>();
        foreach (var amount in amounts)
        {
            var one = Parse(amount);
            if (one == null) return null;
            parsed.Add(one);
        }

        if (parsed.Count == 0) return null;

        var unit = parsed[0].Unit;
        if (parsed.Any(p => !string.Equals(p.Unit.TrimEnd('.'), unit.TrimEnd('.'), StringComparison.OrdinalIgnoreCase)))
            return null;

        var number = parsed.Sum(p => p.Quantity).ToString("0.##", CultureInfo.InvariantCulture);
        if (parsed.FirstOrDefault(p => p.Separator != null)?.Separator == ',')
            number = number.Replace('.', ',');

        return unit.Length == 0 ? number : $"{number} {unit}";
    }

    private static Parsed? Parse(string? amount)
    {
        if (string.IsNullOrWhiteSpace(amount)) return null;

        var match = AmountPattern().Match(amount.Trim());
        if (!match.Success) return null;

        var number = match.Groups[1].Value;
        var separator = number.Contains(',') ? ',' : number.Contains('.') ? '.' : (char?)null;
        var quantity = decimal.Parse(number.Replace(',', '.'), CultureInfo.InvariantCulture);

        return new Parsed(quantity, match.Groups[2].Value, separator);
    }

    private sealed record Parsed(decimal Quantity, string Unit, char? Separator);
}
