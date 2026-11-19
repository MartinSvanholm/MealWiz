using System.Text.RegularExpressions;

namespace TheMealWorkshop.Shared.Amounts;

/// <summary>
/// An amount typed as text ("200 g", "1,5 dl", "2½", "2"), read as a quantity and a unit. The text stays the source
/// of truth; <see cref="Quantity"/> is null when it can't be read. The SQL function <c>public.parse_amount</c>
/// follows the same rules, and <c>TheMealWorkshop/tests/Contracts/amount_parsing.json</c> pins that they agree.
/// </summary>
/// <remarks>
/// Rules: trim space, tab, CR and LF. A quantity is an integer of 1-6 digits, a decimal with "," or "." and 1-2
/// decimals, a fraction a/b, a mixed "2 1/2", or an optional integer plus one of ½ ¼ ¾ ⅓ ⅔. It rounds to 3
/// decimals (half away from zero) and must be above 0. Then comes an optional unit word and its optional ".".
/// Anything else is unreadable. ASCII classes only: <c>\d</c> and <c>\s</c> are Unicode-aware.
/// </remarks>
public sealed partial record ParsedAmount(string Text, decimal? Quantity, AmountUnit? Unit)
{
    public bool IsUnderstood => Quantity is not null;

    /// <summary>Null when the amount isn't understood; <see cref="UnitFamily.Count"/> when it has no unit.</summary>
    public UnitFamily? Family => IsUnderstood ? Unit?.Family ?? UnitFamily.Count : null;

    public string? UnitCode => Unit?.Code;

    // Every accepted spelling, lower case, without the optional trailing ".".
    private static readonly Dictionary<string, string?> Spellings = BuildSpellings();

    private static Dictionary<string, string?> BuildSpellings()
    {
        var map = new Dictionary<string, string?>();
        void Add(string? code, params string[] words)
        {
            foreach (var word in words) map.Add(word, code);
        }

        Add("g", "g", "gr", "gram", "grams");
        Add("kg", "kg", "kilo", "kilos", "kilogram", "kilograms");
        Add("oz", "oz", "ounce", "ounces");
        Add("lb", "lb", "lbs", "pound", "pounds");
        Add("ml", "ml", "milliliter", "milliliters", "millilitre", "millilitres");
        Add("dl", "dl", "deciliter", "deciliters", "decilitre", "decilitres");
        Add("l", "l", "liter", "liters", "litre", "litres");
        Add("cup", "cup", "cups", "kop", "kopper");
        Add("tsk", "tsk", "teske", "teskeer", "teskefuld", "teskefulde", "tsp", "tsps", "teaspoon", "teaspoons");
        Add("spsk", "spsk", "spiseske", "spiseskeer", "spiseskefuld", "spiseskefulde", "tbsp", "tbsps", "tbs",
            "tablespoon", "tablespoons");
        Add(null, "stk", "styk", "styks", "pc", "pcs", "piece", "pieces");
        return map;
    }

    [GeneratedRegex(
        "^(?:(?<whole>[0-9]{1,6})[ \t]+(?<num>[0-9]{1,6})/(?<den>[0-9]{1,6})" +
        "|(?<num>[0-9]{1,6})/(?<den>[0-9]{1,6})" +
        "|(?<whole>[0-9]{1,6})?[ \t]?(?<glyph>[½¼¾⅓⅔])" +
        "|(?<dec>[0-9]{1,6}(?:[.,][0-9]{1,2})?))" +
        "[ \t]*(?:(?<unit>[A-Za-z]+)[.]?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex AmountPattern();

    /// <summary>Reads <paramref name="text"/>; never null. Unreadable text gives a null quantity and unit.</summary>
    public static ParsedAmount Parse(string? text)
    {
        var original = text ?? string.Empty;
        var match = AmountPattern().Match(original.Trim(' ', '\t', '\r', '\n'));
        if (!match.Success) return new ParsedAmount(original, null, null);

        var quantity = ReadQuantity(match);
        if (quantity is null) return new ParsedAmount(original, null, null);

        AmountUnit? unit = null;
        if (match.Groups["unit"].Success)
        {
            if (!Spellings.TryGetValue(match.Groups["unit"].Value.ToLowerInvariant(), out var code))
                return new ParsedAmount(original, null, null);
            unit = AmountUnit.FromCode(code);
        }

        return new ParsedAmount(original, quantity, unit);
    }

    private static decimal? ReadQuantity(Match match)
    {
        decimal value;
        if (match.Groups["dec"].Success)
        {
            value = decimal.Parse(match.Groups["dec"].Value.Replace(',', '.'), System.Globalization.CultureInfo.InvariantCulture);
        }
        else
        {
            var whole = match.Groups["whole"].Success ? decimal.Parse(match.Groups["whole"].Value, System.Globalization.CultureInfo.InvariantCulture) : 0m;
            if (match.Groups["glyph"].Success)
            {
                value = whole + match.Groups["glyph"].Value switch
                {
                    "½" => 0.5m,
                    "¼" => 0.25m,
                    "¾" => 0.75m,
                    "⅓" => 1m / 3m,
                    _ => 2m / 3m
                };
            }
            else
            {
                var den = decimal.Parse(match.Groups["den"].Value, System.Globalization.CultureInfo.InvariantCulture);
                if (den == 0) return null;
                value = whole + decimal.Parse(match.Groups["num"].Value, System.Globalization.CultureInfo.InvariantCulture) / den;
            }
        }

        value = Math.Round(value, 3, MidpointRounding.AwayFromZero);
        return value > 0 ? value : null;
    }
}
