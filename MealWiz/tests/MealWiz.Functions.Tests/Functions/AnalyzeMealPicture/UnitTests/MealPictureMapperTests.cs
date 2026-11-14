using MealWiz.Functions.Functions.AnalyzeMealPicture;
using MealWiz.Functions.Shared.Models;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.AnalyzeMealPicture.UnitTests;

public class MealPictureMapperTests
{
    [Fact]
    public void WrittenRecipe_IsTrimmedAndNotGuessed()
    {
        var result = MealPictureMapper.Map(new MealPictureReading
        {
            Kind = MealPictureKind.WrittenRecipe,
            Name = "  Pasta ",
            Recipe = "Boil.\r\n\r\n  Serve.  \r",
            Ingredients =
            [
                new() { Amount = " 500 g ", Name = " pasta " },
                new() { Amount = "1", Name = "   " },
                new() { Amount = "", Name = "salt" }
            ]
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("Pasta", result.Value.Name);
        Assert.Equal("Boil.\nServe.", result.Value.Recipe);
        Assert.False(result.Value.IsGuessed);
        Assert.Collection(result.Value.Ingredients,
            i => Assert.Equal(("500 g", "pasta"), (i.Amount, i.Name)),
            i => Assert.Equal(("", "salt"), (i.Amount, i.Name)));
    }

    [Fact]
    public void Dish_IsGuessed() =>
        Assert.True(MealPictureMapper.Map(new MealPictureReading { Kind = MealPictureKind.Dish, Name = "Lasagne" }).Value.IsGuessed);

    [Theory]
    [InlineData(MealPictureKind.NoMeal, "Pasta")]
    [InlineData(MealPictureKind.WrittenRecipe, "   ")]
    [InlineData(MealPictureKind.Dish, "")]
    public void NoMealOrBlankName_IsNoMealFound(MealPictureKind kind, string name)
    {
        var result = MealPictureMapper.Map(new MealPictureReading { Kind = kind, Name = name });

        Assert.Equal(RecipeImportErrorCode.NoMealFound, Assert.IsType<PictureImportError>(Assert.Single(result.Errors)).Code);
    }
}
