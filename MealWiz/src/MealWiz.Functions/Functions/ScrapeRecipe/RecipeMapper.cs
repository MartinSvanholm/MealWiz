using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Ingredients;
using MealWiz.Functions.Shared.Models;

namespace MealWiz.Functions.Functions.ScrapeRecipe;

public interface IRecipeMapper
{
    MealImportPayload Map(ParsedRecipe parsedRecipe);
}

public class RecipeMapper(IIngredientLineParser ingredientLineParser) : IRecipeMapper
{
    public MealImportPayload Map(ParsedRecipe parsedRecipe) => new()
    {
        Name = parsedRecipe.Name,
        Recipe = parsedRecipe.Recipe ?? string.Empty,
        Ingredients = parsedRecipe.Ingredients
            .Select(ingredient => ingredientLineParser.Parse(ingredient.Name))
            .Select(line => new MealImportIngredientPayload { Name = line.Name, Amount = line.Amount })
            .ToList()
    };
}
