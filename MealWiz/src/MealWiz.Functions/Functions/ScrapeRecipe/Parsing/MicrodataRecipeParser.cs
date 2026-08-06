using AngleSharp.Dom;

namespace MealWiz.Functions.Functions.ScrapeRecipe.Parsing;

public interface IMicrodataRecipeParser
{
    ParsedRecipe? TryParse(IDocument document);
}

/// <summary>
/// Extracts a schema.org Recipe from Microdata markup (itemscope/itemtype/itemprop attributes),
/// scoped so a nested itemprop belonging to a different itemscope (e.g. a nested Person or
/// AggregateRating) doesn't leak into the recipe's own fields.
/// </summary>
public class MicrodataRecipeParser : IMicrodataRecipeParser
{
    public ParsedRecipe? TryParse(IDocument document)
    {
        var recipeScope = document.QuerySelectorAll("[itemscope][itemtype]")
            .FirstOrDefault(el => (el.GetAttribute("itemtype") ?? string.Empty)
                .Contains("schema.org/Recipe", StringComparison.OrdinalIgnoreCase));

        if (recipeScope is null) return null;

        var name = GetItemPropText(recipeScope, "name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var ingredients = GetItemPropElements(recipeScope, "recipeIngredient")
            .Select(el => el.TextContent.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => new ParsedIngredient { Name = text })
            .ToList();

        var instructionSteps = GetItemPropElements(recipeScope, "recipeInstructions")
            .Select(el => el.TextContent.Trim())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();

        return new ParsedRecipe
        {
            Name = name.Trim(),
            Recipe = instructionSteps.Count > 0 ? string.Join('\n', instructionSteps) : null,
            Ingredients = ingredients
        };
    }

    private static string? GetItemPropText(IElement scope, string propertyName) =>
        GetItemPropElements(scope, propertyName).FirstOrDefault()?.TextContent;

    private static IEnumerable<IElement> GetItemPropElements(IElement scope, string propertyName) =>
        scope.QuerySelectorAll($"[itemprop='{propertyName}']")
            .Where(el => FindNearestItemScopeAncestor(el) == scope);

    private static IElement? FindNearestItemScopeAncestor(IElement element)
    {
        var current = element.ParentElement;
        while (current is not null)
        {
            if (current.HasAttribute("itemscope")) return current;
            current = current.ParentElement;
        }

        return null;
    }
}
