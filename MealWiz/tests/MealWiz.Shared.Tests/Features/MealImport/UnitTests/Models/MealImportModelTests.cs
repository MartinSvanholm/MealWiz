using MealWiz.Shared.Features.MealImport.Models;
using Newtonsoft.Json;
using Xunit;
using MealImportModel = MealWiz.Shared.Features.MealImport.Models.MealImport;

namespace MealWiz.Shared.Tests.Features.MealImport.UnitTests.Models;

public class MealImportModelTests
{
    [Theory]
    [InlineData(MealImportStatus.Pending, "pending")]
    [InlineData(MealImportStatus.Succeeded, "succeeded")]
    [InlineData(MealImportStatus.Failed, "failed")]
    public void Status_RoundTripsThroughDbValue(MealImportStatus status, string dbValue)
    {
        Assert.Equal(dbValue, status.ToDbValue());
        Assert.Equal(status, MealImportStatusExtensions.FromDbValue(dbValue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void Status_UnknownDbValue_FallsBackToPending(string? dbValue)
    {
        Assert.Equal(MealImportStatus.Pending, MealImportStatusExtensions.FromDbValue(dbValue));
    }

    [Fact]
    public void MealImport_RoundTripsThroughDbModel()
    {
        var original = new MealImportModel
        {
            Id = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            SourceUrl = "https://example.com/recipe",
            Status = MealImportStatus.Failed,
            ParsedMeal = new ParsedMeal { Name = "Pasta" },
            ErrorMessage = "Nope",
            CreatedAt = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 12, 8, 1, 0, DateTimeKind.Utc),
            UpdatedBy = Guid.NewGuid()
        };

        var db = original.MapToMealImportDb();
        var roundTripped = new MealImportModel(db);

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

    // The golden files in tests/Contracts are what MealWiz.Functions writes (its ContractTests check that side).
    [Fact]
    public void ParsedMeal_DeserializesTheFunctionsPayloadShape()
    {
        var parsed = JsonConvert.DeserializeObject<ParsedMeal>(ReadContract("parsed_meal.json"))!;

        Assert.Equal("Æblepandekager", parsed.Name);
        Assert.Equal("Pisk dejen.\nSteg pandekagerne.", parsed.Recipe);
        Assert.Collection(parsed.Ingredients,
            i => { Assert.Equal("sødmælk", i.Name); Assert.Equal("2½ dl", i.Amount); },
            i => { Assert.Equal("salt", i.Name); Assert.Equal("", i.Amount); });
    }

    [Fact]
    public void Status_DbValuesMatchTheFunctionsContract()
    {
        var contract = JsonConvert.DeserializeObject<Dictionary<string, string>>(ReadContract("meal_import_status.json"))!;

        Assert.Equal(contract.Keys.Order(), Enum.GetNames<MealImportStatus>().Order());
        foreach (var (name, dbValue) in contract)
        {
            var status = Enum.Parse<MealImportStatus>(name);
            Assert.Equal(dbValue, status.ToDbValue());
            Assert.Equal(status, MealImportStatusExtensions.FromDbValue(dbValue));
        }
    }

    private static string ReadContract(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", fileName));

    [Fact]
    public void ToMeal_BuildsAnUnsavedMealWithIngredientsOnTheGroceryList()
    {
        var parsed = new ParsedMeal
        {
            Name = "Pasta",
            Recipe = "Boil\nServe",
            Ingredients =
            [
                new ParsedMealIngredient { Name = "pasta", Amount = "500 g" },
                new ParsedMealIngredient { Name = "salt", Amount = "" }
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
