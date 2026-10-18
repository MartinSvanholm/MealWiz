using AngleSharp.Dom;

namespace MealWiz.Functions.Functions.ScrapeRecipe.Parsing;

public interface IOpenGraphRecipeParser
{
    ParsedRecipe? TryParse(IDocument document);
}

/// <summary>
/// Last-resort fallback when a page has no schema.org Recipe markup at all. OpenGraph tags carry no
/// structured ingredients/instructions, so this only ever produces a title (and, best-effort, a
/// description as free-form recipe text) — good enough to seed the pre-fill form for manual completion.
/// </summary>
public class OpenGraphRecipeParser : IOpenGraphRecipeParser
{
    public ParsedRecipe? TryParse(IDocument document)
    {
        var title = document.QuerySelector("meta[property='og:title']")?.GetAttribute("content");
        if (string.IsNullOrWhiteSpace(title)) return null;

        var description = document.QuerySelector("meta[property='og:description']")?.GetAttribute("content");

        return new ParsedRecipe
        {
            Name = title.Trim(),
            Recipe = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Ingredients = []
        };
    }
}
