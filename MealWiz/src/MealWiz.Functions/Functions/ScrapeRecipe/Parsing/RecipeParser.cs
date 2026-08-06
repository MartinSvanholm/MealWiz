using AngleSharp;

namespace MealWiz.Functions.Functions.ScrapeRecipe.Parsing;

public interface IRecipeParser
{
    Task<ParsedRecipe?> ParseAsync(string html);
}

/// <summary>
/// Tries JSON-LD first (most reliable, most common), then Microdata, then OpenGraph as a last resort.
/// Returns null if none of the three tiers found anything usable.
/// </summary>
public class RecipeParser(
    IJsonLdRecipeParser jsonLdParser,
    IMicrodataRecipeParser microdataParser,
    IOpenGraphRecipeParser openGraphParser) : IRecipeParser
{
    public async Task<ParsedRecipe?> ParseAsync(string html)
    {
        var context = BrowsingContext.New(Configuration.Default);
        var document = await context.OpenAsync(req => req.Content(html));

        return jsonLdParser.TryParse(document)
            ?? microdataParser.TryParse(document)
            ?? openGraphParser.TryParse(document);
    }
}
