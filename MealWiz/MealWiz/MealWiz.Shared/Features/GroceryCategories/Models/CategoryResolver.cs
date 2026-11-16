using System.Text;

namespace MealWiz.Shared.Features.GroceryCategories.Models;

/// <summary>
/// Places a grocery name in a category from the name alone, through <see cref="CategoryDictionary"/>. Pure: no I/O, and
/// the same answer in every app language. A category is never stored on an item; it is resolved here each time.
/// </summary>
public static class CategoryResolver
{
    /// <summary>The shortest dictionary word a compound may end in ("ost" is too short: "kost" is not cheese).</summary>
    public const int MinCompoundEnding = 4;

    /// <summary>
    /// The default category for <paramref name="name"/>, first hit wins: (1) the whole name; (2) each word, last to
    /// first; (3) each word, last to first, that ends with a dictionary word of <see cref="MinCompoundEnding"/> or more
    /// letters, the longest ending winning (Danish compounds: "cherrytomater"); (4) <see cref="DefaultCategoryKey.Other"/>.
    /// Case, surrounding spaces and runs of spaces don't matter.
    /// </summary>
    public static DefaultCategoryKey ResolveKey(string? name)
    {
        var words = CategoryDictionary.Words;
        var normalised = Normalise(name);
        if (normalised.Length == 0) return DefaultCategoryKey.Other;
        if (words.TryGetValue(normalised, out var whole)) return whole;

        var parts = Words(normalised);
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            if (words.TryGetValue(parts[i], out var key)) return key;
        }

        for (var i = parts.Count - 1; i >= 0; i--)
        {
            if (LongestEnding(parts[i]) is { } key) return key;
        }

        return DefaultCategoryKey.Other;
    }

    /// <summary>
    /// The user's category for <paramref name="name"/>: the category the user remembered for the whole normalised name
    /// (when it is among <paramref name="categories"/>); else the category that takes the dictionary key, by its own
    /// default key or one it absorbed; else the user's Other; else null (no categories loaded).
    /// </summary>
    public static GroceryCategory? Resolve(
        string? name,
        IReadOnlyList<GroceryCategory> categories,
        IReadOnlyDictionary<string, int>? rememberedNames = null)
    {
        if (categories.Count == 0) return null;

        if (rememberedNames is not null
            && rememberedNames.TryGetValue(Normalise(name), out var rememberedId)
            && categories.FirstOrDefault(c => c.Id == rememberedId) is { } remembered)
            return remembered;

        var key = ResolveKey(name);
        return categories.FirstOrDefault(c => c.DefaultKey == key)
            ?? categories.FirstOrDefault(c => c.Takes(key))
            ?? categories.FirstOrDefault(c => c.IsOther);
    }

    /// <summary>Trimmed, lower case (invariant), inner runs of whitespace collapsed to one space.</summary>
    public static string Normalise(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return string.Empty;
        return string.Join(' ', name.Trim().ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    // Words split on whitespace and punctuation; digits stay ("3000"), so they simply never match.
    private static List<string> Words(string normalised)
    {
        var words = new List<string>();
        var current = new StringBuilder();
        foreach (var c in normalised)
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                words.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0) words.Add(current.ToString());
        return words;
    }

    private static DefaultCategoryKey? LongestEnding(string word)
    {
        // Try the longest proper ending first, down to the shortest allowed.
        for (var start = 1; start <= word.Length - MinCompoundEnding; start++)
        {
            if (CategoryDictionary.Words.TryGetValue(word[start..], out var key)) return key;
        }

        return null;
    }
}
