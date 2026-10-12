using MealWiz.Shared.Features.RecipeImport.Models;
using Newtonsoft.Json;
using Xunit;
using RecipeImportModel = MealWiz.Shared.Features.RecipeImport.Models.RecipeImport;

namespace MealWiz.Shared.Tests.Features.RecipeImport.UnitTests.Models;

public class RecipeImportModelTests
{
    [Theory]
    [InlineData(RecipeImportStatus.Pending, "pending")]
    [InlineData(RecipeImportStatus.Succeeded, "succeeded")]
    [InlineData(RecipeImportStatus.Failed, "failed")]
    public void Status_RoundTripsThroughDbValue(RecipeImportStatus status, string dbValue)
    {
        Assert.Equal(dbValue, status.ToDbValue());
        Assert.Equal(status, RecipeImportStatusExtensions.FromDbValue(dbValue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void Status_UnknownDbValue_FallsBackToPending(string? dbValue)
    {
        Assert.Equal(RecipeImportStatus.Pending, RecipeImportStatusExtensions.FromDbValue(dbValue));
    }

    [Fact]
    public void RecipeImport_RoundTripsThroughDbModel()
    {
        var original = new RecipeImportModel
        {
            Id = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            SourceUrl = "https://example.com/recipe",
            Status = RecipeImportStatus.Failed,
            ParsedMeal = new ParsedRecipeMeal { Name = "Pasta" },
            ErrorMessage = "Nope",
            CreatedAt = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 12, 8, 1, 0, DateTimeKind.Utc),
            UpdatedBy = Guid.NewGuid()
        };

        var db = original.MapToRecipeImportDb();
        var roundTripped = new RecipeImportModel(db);

        Assert.Equal("failed", db.Status);
        Assert.Equal(original.Id, roundTripped.Id);
        Assert.Equal(original.CreatedBy, roundTripped.CreatedBy);
        Assert.Equal(original.SourceUrl, roundTripped.SourceUrl);
        Assert.Equal(original.Status, roundTripped.Status);
        Assert.Same(original.ParsedMeal, roundTripped.ParsedMeal);
        Assert.Equal(original.ErrorMessage, roundTripped.ErrorMessage);
        Assert.Equal(original.CreatedAt, roundTripped.CreatedAt);
        Assert.Equal(original.UpdatedAt, roundTripped.UpdatedAt);
        Assert.Equal(original.UpdatedBy, roundTripped.UpdatedBy);
    }

    [Fact]
    public void ParsedRecipeMeal_DeserializesTheFunctionsPayloadShape()
    {
        // As written by MealWiz.Functions' MealImportPayload (PascalCase, Amount may be empty).
        const string json = """
            {"Name":"Pasta","Recipe":"Boil\nServe","Ingredients":[{"Name":"pasta","Amount":"500 g"},{"Name":"salt","Amount":""}]}
            """;

        var parsed = JsonConvert.DeserializeObject<ParsedRecipeMeal>(json)!;

        Assert.Equal("Pasta", parsed.Name);
        Assert.Equal("Boil\nServe", parsed.Recipe);
        Assert.Collection(parsed.Ingredients,
            i => { Assert.Equal("pasta", i.Name); Assert.Equal("500 g", i.Amount); },
            i => { Assert.Equal("salt", i.Name); Assert.Equal("", i.Amount); });
    }

    [Fact]
    public void ToMeal_BuildsAnUnsavedMealWithIngredientsOnTheGroceryList()
    {
        var parsed = new ParsedRecipeMeal
        {
            Name = "Pasta",
            Recipe = "Boil\nServe",
            Ingredients =
            [
                new ParsedRecipeIngredient { Name = "pasta", Amount = "500 g" },
                new ParsedRecipeIngredient { Name = "salt", Amount = "" }
            ]
        };

        var meal = parsed.ToMeal();

        Assert.Equal(0, meal.Id);
        Assert.Equal("Pasta", meal.Name);
        Assert.Equal(new[] { "Boil", "Serve" }, meal.RecipeSteps);
        Assert.Collection(meal.Ingredients,
            i => { Assert.Equal("pasta", i.Name); Assert.Equal("500 g", i.Amount); Assert.True(i.ShowOnGroceryList); Assert.Equal(0, i.Id); },
            i => { Assert.Equal("salt", i.Name); Assert.Equal("", i.Amount); Assert.True(i.ShowOnGroceryList); Assert.Equal(0, i.Id); });
    }
}
