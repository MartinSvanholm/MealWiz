using System.Text.RegularExpressions;

namespace MealWiz.Functions.Shared.Ingredients;

public record IngredientLine(string Amount, string Name);

public interface IIngredientLineParser
{
    IngredientLine Parse(string line);
}

/// <summary>
/// Splits a free-text ingredient line ("2½ dl sødmælk, stuetempereret") into an amount ("2½ dl") and a
/// name ("sødmælk, stuetempereret"). schema.org's recipeIngredient is plain text with no separate amount
/// field, so this is a heuristic over Danish and English lines, reading from the start of the line:
///
///   [approximation] quantity [(size)] [unit] [of] name
///
/// - approximation: "ca.", "cirka", "about", … — kept in the amount as written.
/// - quantity: digits, decimals ("1,5"), fractions ("1/2", "½", "1 1/2", "2½"), ranges ("2-3", "2 til 3"),
///   or number words ("et", "to", "three", "halvanden", "en halv", "half a"), which are converted to digits.
/// - (size): a parenthetical right after the quantity, as in "1 (400 g) dåse tomater".
/// - unit: a known Danish/English unit (g, dl, spsk, tsk, knivspids, cups, cloves, …).
///
/// When there's no leading quantity, or nothing would be left for the name, the whole line becomes the name
/// and the amount stays empty. A missed split is harmless (the user reviews the pre-fill form), so the rules
/// err on the side of not splitting.
/// </summary>
public partial class IngredientLineParser : IIngredientLineParser
{
    private static readonly Dictionary<string, int> NumberWords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = 1, ["et"] = 1, ["a"] = 1, ["an"] = 1, ["one"] = 1,
        ["to"] = 2, ["two"] = 2,
        ["tre"] = 3, ["three"] = 3,
        ["fire"] = 4, ["four"] = 4,
        ["fem"] = 5, ["five"] = 5,
        ["seks"] = 6, ["six"] = 6,
        ["syv"] = 7, ["seven"] = 7,
        ["otte"] = 8, ["eight"] = 8,
        ["ni"] = 9, ["nine"] = 9,
        ["ti"] = 10, ["ten"] = 10,
        ["elleve"] = 11, ["eleven"] = 11,
        ["tolv"] = 12, ["twelve"] = 12
    };

    private static readonly HashSet<string> Articles = new(StringComparer.OrdinalIgnoreCase) { "en", "et", "a", "an" };

    // After an article, these mean "some" rather than "one" ("a little oil", "et par æg"), so the line stays whole.
    private static readonly HashSet<string> NonCountingAfterArticle = new(StringComparer.OrdinalIgnoreCase)
    {
        "little", "few", "lot", "bit", "couple", "par", "del", "anelse"
    };

    private const string NumberWordPattern =
        "en|et|an|a|one|to|two|tre|three|fire|four|fem|five|seks|six|syv|seven|otte|eight|ni|nine|ti|ten|elleve|eleven|tolv|twelve";

    public IngredientLine Parse(string line)
    {
        var text = line.Trim();
        var unsplit = new IngredientLine(string.Empty, text);

        var position = 0;
        var amountStart = 0;

        var approximation = ApproximationRegex().Match(text, position);
        if (approximation.Success) position = approximation.Index + approximation.Length;

        var quantityStart = position;
        string quantityText;

        var digitQuantity = DigitQuantityRegex().Match(text, position);
        if (digitQuantity.Success)
        {
            quantityText = digitQuantity.Value;
            position = digitQuantity.Index + digitQuantity.Length;
        }
        else if (TryMatchWordQuantity(text, position, out var wordQuantityText, out var wordQuantityEnd))
        {
            quantityText = wordQuantityText;
            position = wordQuantityEnd;
        }
        else
        {
            return unsplit;
        }

        var quantityEnd = position;

        var size = SizeRegex().Match(text, position);
        if (size.Success) position = size.Index + size.Length;

        var unit = UnitRegex().Match(text, position);
        if (unit.Success) position = unit.Index + unit.Length;

        // A quantity glued to a word that isn't a unit ("7up") isn't a quantity.
        if (!size.Success && !unit.Success && position < text.Length && char.IsLetter(text[position]))
        {
            return unsplit;
        }

        var amountEnd = position;

        if (unit.Success)
        {
            var connector = ConnectorRegex().Match(text, position);
            if (connector.Success) position = connector.Index + connector.Length;
        }

        var name = text[position..].Trim();
        if (name.Length == 0) return unsplit;

        var amount = text[amountStart..quantityStart] + quantityText + text[quantityEnd..amountEnd];
        return new IngredientLine(amount.Trim(), name);
    }

    private static bool TryMatchWordQuantity(string text, int position, out string quantityText, out int end)
    {
        quantityText = string.Empty;
        end = position;

        var oneAndAHalf = OneAndAHalfRegex().Match(text, position);
        if (oneAndAHalf.Success)
        {
            (quantityText, end) = ("1½", oneAndAHalf.Index + oneAndAHalf.Length);
            return true;
        }

        var wholeAndAHalf = WholeAndAHalfRegex().Match(text, position);
        if (wholeAndAHalf.Success)
        {
            (quantityText, end) = ($"{NumberWords[wholeAndAHalf.Groups["whole"].Value]}½", wholeAndAHalf.Index + wholeAndAHalf.Length);
            return true;
        }

        var half = HalfRegex().Match(text, position);
        if (half.Success)
        {
            (quantityText, end) = ("½", half.Index + half.Length);
            return true;
        }

        var word = NumberWordRegex().Match(text, position);
        if (!word.Success) return false;

        var wordEnd = word.Index + word.Length;
        if (Articles.Contains(word.Value) && NonCountingAfterArticle.Contains(NextWord(text, wordEnd)))
        {
            return false;
        }

        (quantityText, end) = (NumberWords[word.Value].ToString(), wordEnd);
        return true;
    }

    private static string NextWord(string text, int position)
    {
        var next = NextWordRegex().Match(text, position);
        return next.Success ? next.Groups["word"].Value : string.Empty;
    }

    [GeneratedRegex(@"\G(?:ca\.|ca|cirka|circa|omkring|approx\.|approximately|approx|about|around)(?!\p{L})\s*", RegexOptions.IgnoreCase)]
    private static partial Regex ApproximationRegex();

    // Atomic group so "38 %" can't backtrack to "3" and slip past the percent check.
    [GeneratedRegex(@"\G(?>(?:\d+\s+\d+/\d+|\d+/\d+|\d+(?:[.,]\d+)?(?:\s?[½⅓⅔¼¾⅛⅜⅝⅞])?|[½⅓⅔¼¾⅛⅜⅝⅞])(?:\s*(?:-|–|til|to)\s*(?:\d+(?:[.,]\d+)?|[½⅓⅔¼¾⅛⅜⅝⅞]))?)(?!\s*%)", RegexOptions.IgnoreCase)]
    private static partial Regex DigitQuantityRegex();

    [GeneratedRegex(@"\Ghalvanden(?!\p{L})", RegexOptions.IgnoreCase)]
    private static partial Regex OneAndAHalfRegex();

    [GeneratedRegex(@"\G(?<whole>" + NumberWordPattern + @")\s+(?:og|and)\s+(?:en|et|a)\s+(?:halvt?|half)(?!\p{L})", RegexOptions.IgnoreCase)]
    private static partial Regex WholeAndAHalfRegex();

    [GeneratedRegex(@"\G(?:(?:en|et)\s+halvt?|a\s+half|half\s+an?|halvt?|half)(?!\p{L})", RegexOptions.IgnoreCase)]
    private static partial Regex HalfRegex();

    [GeneratedRegex(@"\G(?:" + NumberWordPattern + @")(?=\s)", RegexOptions.IgnoreCase)]
    private static partial Regex NumberWordRegex();

    [GeneratedRegex(@"\G\s*\([^()]*\)")]
    private static partial Regex SizeRegex();

    [GeneratedRegex(@"\G\s*(?:fl\.?\s?oz|kilograms?|kilo|kg|grams?|gram|mg|g|liters?|litres?|liter|l|dl|cl|milliliters?|millilitres?|ml|spsk|tsk|knsp|knivspids(?:er)?|stk|stykker?|fed|dåser?|pakker?|pk|bundt(?:er)?|skiver?|håndfulde?|nip|glas|poser?|bakker?|smule|dråber?|kviste?|blade?|stilke?|dusin|tablespoons?|tbsp|tbs|tbl|teaspoons?|tsp|cups?|oz|ounces?|lbs?|pounds?|pints?|quarts?|gallons?|pinch(?:es)?|dash(?:es)?|cloves?|cans?|tins?|slices?|handfuls?|sprigs?|bunch(?:es)?|stalks?|packages?|packets?|sticks?|drops?|pieces?|dozen)(?!\p{L})\.?", RegexOptions.IgnoreCase)]
    private static partial Regex UnitRegex();

    [GeneratedRegex(@"\G\s+of(?!\p{L})", RegexOptions.IgnoreCase)]
    private static partial Regex ConnectorRegex();

    [GeneratedRegex(@"\G\s+(?<word>\p{L}+)")]
    private static partial Regex NextWordRegex();
}
