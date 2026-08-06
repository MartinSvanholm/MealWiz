using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Models;

namespace MealWiz.Functions.Functions.ScrapeRecipe;

public interface IRecipeMapper
{
    MealImportPayload Map(ParsedRecipe parsedRecipe);
}

public class RecipeMapper : IRecipeMapper
{
    public MealImportPayload Map(ParsedRecipe parsedRecipe) => new()
    {
        Name = parsedRecipe.Name,
        Recipe = parsedRecipe.Recipe ?? string.Empty,
        Ingredients = parsedRecipe.Ingredients
            .Select(ingredient => new MealImportIngredientPayload { Name = ingredient.Name })
            .ToList()
    };
}
