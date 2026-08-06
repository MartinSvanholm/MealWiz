using MealWiz.Functions.Functions.ScrapeRecipe;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.ScrapeRecipe;

public class RecipeMapperTests
{
    private readonly RecipeMapper _mapper = new();

    [Fact]
    public void Map_CopiesNameAndRecipeAndKeepsIngredientLinesWhole()
    {
        var parsed = new ParsedRecipe
        {
            Name = "Test Recipe",
            Recipe = "Step 1\nStep 2",
            Ingredients = [new ParsedIngredient { Name = "1 cup flour, sifted" }]
        };

        var payload = _mapper.Map(parsed);

        Assert.Equal("Test Recipe", payload.Name);
        Assert.Equal("Step 1\nStep 2", payload.Recipe);
        var ingredient = Assert.Single(payload.Ingredients);
        Assert.Equal("1 cup flour, sifted", ingredient.Name);
        Assert.Equal(string.Empty, ingredient.Amount);
    }

    [Fact]
    public void Map_NullRecipeText_MapsToEmptyString()
    {
        var parsed = new ParsedRecipe { Name = "No Instructions", Recipe = null, Ingredients = [] };

        var payload = _mapper.Map(parsed);

        Assert.Equal(string.Empty, payload.Recipe);
        Assert.Empty(payload.Ingredients);
    }
}
