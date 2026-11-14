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
            ErrorCode = MealImportErrorCode.NoRecipeFound,
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
        Assert.Equal("no_recipe_found", db.ErrorCode);
        Assert.Equal(original.ErrorCode, roundTripped.ErrorCode);
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
        Assert.True(parsed.IsGuessed);
        Assert.Collection(parsed.Ingredients,
            i => { Assert.Equal("sødmælk", i.Name); Assert.Equal("2½ dl", i.Amount); },
            i => { Assert.Equal("salt", i.Name); Assert.Equal("", i.Amount); });
    }

    [Fact]
    public void ParsedMeal_WithoutIsGuessed_ReadsAsNotGuessed()
    {
        var parsed = JsonConvert.DeserializeObject<ParsedMeal>("""{"Name":"Pasta"}""")!;

        Assert.False(parsed.IsGuessed);
    }

    [Fact]
    public void PictureImport_RoundTripsThroughDbModel()
    {
        var original = new MealImportModel
        {
            Id = Guid.NewGuid(),
            CreatedBy = Guid.NewGuid(),
            SourceUrl = null,
            PicturePaths = ["u/j/0.jpg", "u/j/1.jpg"],
            Language = "da",
            Hint = "it's lasagne"
        };

        var db = original.MapToMealImportDb();
        var roundTripped = new MealImportModel(db);

        Assert.Null(db.SourceUrl);
        Assert.Equal(new[] { "u/j/0.jpg", "u/j/1.jpg" }, db.PicturePaths);
        Assert.Equal("da", db.Language);
        Assert.Equal("it's lasagne", db.Hint);
        Assert.True(roundTripped.IsPictureImport);
        Assert.Equal(original.PicturePaths, roundTripped.PicturePaths);
        Assert.Equal(original.Language, roundTripped.Language);
        Assert.Equal(original.Hint, roundTripped.Hint);
    }

    [Fact]
    public void LinkImport_IsNotAPictureImport()
    {
        var link = new MealImportModel(new MealImportDb { SourceUrl = "https://example.com/r" });

        Assert.False(link.IsPictureImport);
        Assert.Null(link.PicturePaths);
    }

    [Fact]
    public void PathFor_IsLowercaseUserJobIndexJpg()
    {
        var userId = Guid.Parse("AAAAAAAA-0000-0000-0000-0000000000A1");
        var jobId = Guid.Parse("BBBBBBBB-0000-0000-0000-0000000000B2");

        Assert.Equal("aaaaaaaa-0000-0000-0000-0000000000a1/bbbbbbbb-0000-0000-0000-0000000000b2/2.jpg",
            MealImportPictures.PathFor(userId, jobId, 2));
        Assert.Equal("meal-import-pictures", MealImportPictures.BucketId);
        Assert.Equal(3, MealImportPictures.MaxPictures);
        Assert.Equal(200, MealImportPictures.MaxHintLength);
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

    [Fact]
    public void ErrorCode_DbValuesMatchTheFunctionsContract()
    {
        var contract = JsonConvert.DeserializeObject<Dictionary<string, string>>(ReadContract("meal_import_error_code.json"))!;

        Assert.Equal(contract.Keys.Order(), Enum.GetNames<MealImportErrorCode>().Order());
        foreach (var (name, dbValue) in contract)
        {
            var code = Enum.Parse<MealImportErrorCode>(name);
            Assert.Equal(dbValue, code.ToDbValue());
            Assert.Equal(code, MealImportErrorCodeExtensions.FromDbValue(dbValue));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("site_on_fire")]
    public void ErrorCode_MissingOrUnknownDbValue_IsNull(string? dbValue)
    {
        Assert.Null(MealImportErrorCodeExtensions.FromDbValue(dbValue));
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
