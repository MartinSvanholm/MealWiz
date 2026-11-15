using System.Globalization;
using System.Text.RegularExpressions;
using MealWiz.Shared.Amounts;

namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>
/// Adds and subtracts grocery amounts typed as text ("200 g", "0,5 kg"), read through <see cref="ParsedAmount"/>.
/// Amounts combine when all are understood and in one family: mass and volume convert exactly, a count, cup, tsk or
/// spsk only adds to itself. The result is written by <see cref="AmountText.Format"/> in the unit that reads best.
/// Amounts the parser doesn't understand still combine when each is a number and the same word ("3 cloves" +
/// "2 cloves"; case, a trailing "." and a plural "s" ignored). Anything else gives null, so the user types the total
/// or the amount stays as it is.
/// </summary>
public static partial class AmountHelper
{
    // A number and one word the parser doesn't know ("3 cloves", "1,5 fed").
    [GeneratedRegex(@"^([0-9]{1,6}(?:[.,][0-9]{1,2})?)[ \t]*(\p{L}+\.?)$", RegexOptions.CultureInvariant)]
    private static partial Regex WordAmountPattern();

    private static readonly string[] MetricMass = ["g", "kg"];
    private static readonly string[] ImperialMass = ["oz", "lb"];
    private static readonly string[] MetricVolume = ["ml", "dl", "l"];

    /// <summary>The sum of the amounts, or null when they can't be added.</summary>
    public static string? SuggestTotal(IEnumerable<string?> amounts)
    {
        var list = amounts.ToList();
        if (list.Count == 0) return null;

        return Combine(list.Select(a => (a, 1)).ToList());
    }

    /// <summary>item − old + new, or null when they can't be combined or the result isn't above zero.</summary>
    public static string? Adjust(string? itemAmount, string? oldAmount, string? newAmount) =>
        Combine([(itemAmount, 1), (oldAmount, -1), (newAmount, 1)]);

    /// <summary>item − removed, or null (leave the amount unchanged) when they can't be combined or the result isn't above zero.</summary>
    public static string? Subtract(string? itemAmount, string? removedAmount) =>
        Combine([(itemAmount, 1), (removedAmount, -1)]);

    private static string? Combine(IReadOnlyList<(string? Text, int Sign)> terms)
    {
        if (terms.Any(t => string.IsNullOrWhiteSpace(t.Text))) return null;

        var parsed = terms.Select(t => ParsedAmount.Parse(t.Text)).ToList();
        if (parsed.All(p => p.IsUnderstood)) return CombineUnderstood(parsed, terms);
        if (parsed.All(p => !p.IsUnderstood)) return CombineSameWord(terms);
        return null;
    }

    private static string? CombineUnderstood(IReadOnlyList<ParsedAmount> parsed, IReadOnlyList<(string? Text, int Sign)> terms)
    {
        var family = parsed[0].Family;
        if (parsed.Any(p => p.Family != family)) return null;

        // Cup, tsk and spsk have one unit and a factor of 1; a count has no unit.
        var total = 0m;
        for (var i = 0; i < parsed.Count; i++)
            total += terms[i].Sign * parsed[i].Quantity!.Value * (parsed[i].Unit?.Factor ?? 1m);
        if (total <= 0) return null;

        if (family is not (UnitFamily.Mass or UnitFamily.Volume))
            return AmountText.Format(Math.Round(total, 2, MidpointRounding.AwayFromZero), parsed[0].Unit);

        var (quantity, unit) = InBestUnit(total, Candidates(family.Value, parsed));
        return AmountText.Format(quantity, unit);
    }

    /// <summary>The family's metric units, or oz and lb when every amount is in oz or lb; smallest first.</summary>
    private static List<AmountUnit> Candidates(UnitFamily family, IEnumerable<ParsedAmount> parsed)
    {
        var codes = family == UnitFamily.Volume ? MetricVolume
            : parsed.All(p => ImperialMass.Contains(p.UnitCode)) ? ImperialMass
            : MetricMass;
        return codes.Select(c => AmountUnit.FromCode(c)!).OrderBy(u => u.Factor).ToList();
    }

    /// <summary>
    /// The largest unit whose number is at least 1 and exact to 2 decimals (700 g, 1,2 kg, 7,5 dl); else the smallest
    /// unit, rounded to 2 decimals.
    /// </summary>
    private static (decimal Quantity, AmountUnit Unit) InBestUnit(decimal baseTotal, IReadOnlyList<AmountUnit> smallestFirst)
    {
        foreach (var unit in smallestFirst.Reverse())
        {
            var quantity = baseTotal / unit.Factor;
            if (quantity >= 1 && quantity == Math.Round(quantity, 2)) return (quantity, unit);
        }

        var smallest = smallestFirst[0];
        return (Math.Round(baseTotal / smallest.Factor, 2, MidpointRounding.AwayFromZero), smallest);
    }

    private static string? CombineSameWord(IReadOnlyList<(string? Text, int Sign)> terms)
    {
        var total = 0m;
        string? word = null;
        foreach (var (text, sign) in terms)
        {
            var match = WordAmountPattern().Match(text!.Trim(' ', '\t', '\r', '\n'));
            if (!match.Success) return null;

            var thisWord = match.Groups[2].Value;
            if (word == null) word = thisWord;
            else if (!WordsMatch(word.TrimEnd('.'), thisWord.TrimEnd('.'))) return null;

            total += sign * decimal.Parse(match.Groups[1].Value.Replace(',', '.'), CultureInfo.InvariantCulture);
        }

        if (total <= 0) return null;

        var number = total.ToString("0.##", CultureInfo.CurrentCulture);
        return $"{number} {(total == 1 ? Singular(word!) : word)}";
    }

    /// <summary>Equal ignoring case, or one is the other plus a plural "s" (3+ letters: "clove" = "cloves").</summary>
    private static bool WordsMatch(string a, string b)
    {
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;

        var (shorter, longer) = a.Length < b.Length ? (a, b) : (b, a);
        return shorter.Length >= 3
               && longer.Length == shorter.Length + 1
               && char.ToLowerInvariant(longer[^1]) == 's'
               && string.Equals(longer[..^1], shorter, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>"cloves" → "clove", "Cups" → "Cup"; short words ("tbs"), "-ss" words ("glass") and "stk." stay.</summary>
    private static string Singular(string word) =>
        word.Length >= 4 && (word.EndsWith('s') || word.EndsWith('S'))
                         && !word.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
            ? word[..^1]
            : word;
}
