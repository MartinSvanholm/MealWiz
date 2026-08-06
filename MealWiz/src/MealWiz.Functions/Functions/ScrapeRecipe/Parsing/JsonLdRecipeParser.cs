using System.Text.Json;
using AngleSharp.Dom;

namespace MealWiz.Functions.Functions.ScrapeRecipe.Parsing;

public interface IJsonLdRecipeParser
{
    ParsedRecipe? TryParse(IDocument document);
}

/// <summary>
/// Extracts a schema.org Recipe from any &lt;script type="application/ld+json"&gt; block. Handles a bare
/// Recipe object, an array of objects (picking out the one typed Recipe), and an "@graph" wrapper.
/// <c>recipeInstructions</c> is handled in all three shapes schema.org allows: a plain string, an array
/// of strings, or an array of HowToStep objects (read via their "text" property).
/// </summary>
public class JsonLdRecipeParser : IJsonLdRecipeParser
{
    public ParsedRecipe? TryParse(IDocument document)
    {
        foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
        {
            JsonDocument jsonDocument;
            try
            {
                jsonDocument = JsonDocument.Parse(script.TextContent);
            }
            catch (JsonException)
            {
                continue;
            }

            using (jsonDocument)
            {
                var recipeElement = FindRecipeElement(jsonDocument.RootElement);
                if (recipeElement is { } element)
                {
                    var parsed = TryParseRecipeElement(element);
                    if (parsed is not null) return parsed;
                }
            }
        }

        return null;
    }

    private static JsonElement? FindRecipeElement(JsonElement root)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsRecipeType(root)) return root;

                if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in graph.EnumerateArray())
                    {
                        if (IsRecipeType(item)) return item;
                    }
                }

                return null;

            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                {
                    var found = FindRecipeElement(item);
                    if (found is not null) return found;
                }

                return null;

            default:
                return null;
        }
    }

    private static bool IsRecipeType(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object) return false;
        if (!element.TryGetProperty("@type", out var typeProperty)) return false;

        return typeProperty.ValueKind switch
        {
            JsonValueKind.String => IsRecipe(typeProperty),
            JsonValueKind.Array => typeProperty.EnumerateArray().Any(IsRecipe),
            _ => false
        };

        static bool IsRecipe(JsonElement value) =>
            value.ValueKind == JsonValueKind.String &&
            string.Equals(value.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase);
    }

    private static ParsedRecipe? TryParseRecipeElement(JsonElement element)
    {
        var name = element.TryGetProperty("name", out var nameProperty) && nameProperty.ValueKind == JsonValueKind.String
            ? nameProperty.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(name)) return null;

        return new ParsedRecipe
        {
            Name = name,
            Recipe = ParseInstructions(element),
            Ingredients = ParseIngredients(element)
        };
    }

    private static List<ParsedIngredient> ParseIngredients(JsonElement element)
    {
        var propertyName = element.TryGetProperty("recipeIngredient", out _) ? "recipeIngredient" : "ingredients";

        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => new ParsedIngredient { Name = text! })
            .ToList();
    }

    private static string? ParseInstructions(JsonElement element)
    {
        if (!element.TryGetProperty("recipeInstructions", out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Array => JoinSteps(property),
            _ => null
        };
    }

    private static string? JoinSteps(JsonElement array)
    {
        var steps = array.EnumerateArray()
            .Select(ExtractStep)
            .Where(step => !string.IsNullOrWhiteSpace(step))
            .ToList();

        return steps.Count > 0 ? string.Join('\n', steps) : null;
    }

    private static string? ExtractStep(JsonElement item) => item.ValueKind switch
    {
        JsonValueKind.String => item.GetString(),
        JsonValueKind.Object when item.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String
            => text.GetString(),
        _ => null
    };
}
