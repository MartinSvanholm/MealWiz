using MealWiz.Functions.Functions.ScrapeRecipe;
using MealWiz.Functions.Functions.ScrapeRecipe.Parsing;
using MealWiz.Functions.Shared.Ingredients;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.ScrapeRecipe.UnitTests;

public class RecipeMapperTests
{
    private readonly RecipeMapper _mapper = new(new IngredientLineParser());

    [Fact]
    public void Map_CopiesNameAndRecipeAndSplitsIngredientAmounts()
    {
        var parsed = new ParsedRecipe
        {
            Name = "Test Recipe",
            Recipe = "Step 1\nStep 2",
            Ingredients =
            [
                new ParsedIngredient { Name = "1 cup flour, sifted" },
                new ParsedIngredient { Name = "salt" }
            ]
        };

        var payload = _mapper.Map(parsed);

        Assert.Equal("Test Recipe", payload.Name);
        Assert.Equal("Step 1\nStep 2", payload.Recipe);
        Assert.Collection(payload.Ingredients,
            ingredient =>
            {
                Assert.Equal("1 cup", ingredient.Amount);
                Assert.Equal("flour, sifted", ingredient.Name);
            },
            ingredient =>
            {
                Assert.Equal(string.Empty, ingredient.Amount);
                Assert.Equal("salt", ingredient.Name);
            });
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
