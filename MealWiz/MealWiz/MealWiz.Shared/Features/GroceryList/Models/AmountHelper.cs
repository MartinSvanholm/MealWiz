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

        return Format(parsed.Sum(p => p.Quantity), parsed, unit);
    }

    /// <summary>item − old + new when all three share a unit and the result is above zero, else null.</summary>
    public static string? Adjust(string? itemAmount, string? oldAmount, string? newAmount)
    {
        var item = Parse(itemAmount);
        var old = Parse(oldAmount);
        var current = Parse(newAmount);
        if (item == null || old == null || current == null) return null;

        var unit = item.Unit.TrimEnd('.');
        if (!string.Equals(old.Unit.TrimEnd('.'), unit, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.Unit.TrimEnd('.'), unit, StringComparison.OrdinalIgnoreCase))
            return null;

        var result = item.Quantity - old.Quantity + current.Quantity;
        return result > 0 ? Format(result, [item, old, current], item.Unit) : null;
    }

    /// <summary>
    /// itemAmount minus removedAmount when both parse to the same unit and the result is above zero; else null
    /// (leave the amount unchanged). For this subtraction only, a plural "s" is tolerated ("clove" = "cloves"), and a
    /// result of exactly 1 is written singular ("1 clove"). MEA-99.
    /// </summary>
    public static string? Subtract(string? itemAmount, string? removedAmount)
    {
        var item = Parse(itemAmount);
        var removed = Parse(removedAmount);
        if (item == null || removed == null) return null;
        if (!UnitsMatchIgnoringPlural(item.Unit.TrimEnd('.'), removed.Unit.TrimEnd('.'))) return null;

        var result = item.Quantity - removed.Quantity;
        if (result <= 0) return null;

        var unit = item.Unit.Trim();
        return Format(result, [item, removed], result == 1 ? Singular(unit) : unit);
    }

    private static bool UnitsMatchIgnoringPlural(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;

        var (shorter, longer) = a.Length < b.Length ? (a, b) : (b, a);
        return shorter.Length >= 3
               && longer.Length == shorter.Length + 1
               && char.ToLowerInvariant(longer[^1]) == 's'
               && string.Equals(longer[..^1], shorter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>"cloves" → "clove", "Cups" → "Cup"; short units ("tbs"), "-ss" words ("glass") and "stk." stay.</summary>
    private static string Singular(string unit) =>
        unit.Length >= 4 && (unit.EndsWith('s') || unit.EndsWith('S'))
                         && !unit.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
            ? unit[..^1]
            : unit;

    private static string Format(decimal value, IReadOnlyList<Parsed> inputs, string unit)
    {
        var number = value.ToString("0.##", CultureInfo.InvariantCulture);
        if (inputs.FirstOrDefault(p => p.Separator != null)?.Separator == ',')
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
