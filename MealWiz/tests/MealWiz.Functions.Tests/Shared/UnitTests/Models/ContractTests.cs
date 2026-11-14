using MealWiz.Functions.Shared.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace MealWiz.Functions.Tests.Shared.UnitTests.Models;

/// <summary>
/// The Functions half of the meal_imports contract with the Blazor client. The golden files in
/// tests/Contracts are linked into both test projects; MealImportModelTests in MealWiz.Shared.Tests checks
/// the client half against the same files.
/// </summary>
public class ContractTests
{
    [Fact]
    public void MealImportPayload_SerializesToTheGoldenParsedMeal()
    {
        var payload = new MealImportPayload
        {
            Name = "Æblepandekager",
            Recipe = "Pisk dejen.\nSteg pandekagerne.",
            IsGuessed = true,
            Ingredients =
            [
                new MealImportIngredientPayload { Name = "sødmælk", Amount = "2½ dl" },
                new MealImportIngredientPayload { Name = "salt", Amount = "" }
            ]
        };

        // The serializer settings the Supabase client writes parsed_meal with.
        var json = JsonConvert.SerializeObject(payload, Supabase.Postgrest.Client.SerializerSettings());

        var expected = JToken.Parse(ReadContract("parsed_meal.json"));
        var actual = JToken.Parse(json);
        Assert.True(JToken.DeepEquals(expected, actual), $"Expected {expected}\nbut got {actual}");
    }

    [Fact]
    public void MealImportPayload_WithoutIsGuessed_ReadsAsNotGuessed()
    {
        var payload = JsonConvert.DeserializeObject<MealImportPayload>("""{"Name":"Pasta"}""")!;

        Assert.False(payload.IsGuessed);
    }

    [Fact]
    public void MealImportDb_PictureJob_SerializesItsColumns()
    {
        var row = new MealImportDb
        {
            PicturePaths = ["u/j/0.jpg", "u/j/1.jpg"],
            Language = "da",
            Hint = "lasagne"
        };

        var json = JObject.Parse(JsonConvert.SerializeObject(row, Supabase.Postgrest.Client.SerializerSettings()));

        Assert.Equal(new[] { "u/j/0.jpg", "u/j/1.jpg" }, json["picture_paths"]!.Select(t => (string?)t));
        Assert.Equal("da", (string?)json["language"]);
        Assert.Equal("lasagne", (string?)json["hint"]);
        Assert.Equal(JTokenType.Null, json["source_url"]!.Type);
    }

    [Fact]
    public void RecipeImportStatus_DbValuesMatchTheContract()
    {
        var contract = JsonConvert.DeserializeObject<Dictionary<string, string>>(ReadContract("meal_import_status.json"))!;

        Assert.Equal(contract.Keys.Order(), Enum.GetNames<RecipeImportStatus>().Order());
        foreach (var (name, dbValue) in contract)
        {
            var status = Enum.Parse<RecipeImportStatus>(name);
            Assert.Equal(dbValue, status.ToDbValue());
            Assert.Equal(status, RecipeImportStatusExtensions.FromDbValue(dbValue));
        }
    }

    [Fact]
    public void RecipeImportErrorCode_DbValuesMatchTheContract()
    {
        var contract = JsonConvert.DeserializeObject<Dictionary<string, string>>(ReadContract("meal_import_error_code.json"))!;

        Assert.Equal(contract.Keys.Order(), Enum.GetNames<RecipeImportErrorCode>().Order());
        foreach (var (name, dbValue) in contract)
        {
            var code = Enum.Parse<RecipeImportErrorCode>(name);
            Assert.Equal(dbValue, code.ToDbValue());
            Assert.Equal(code, RecipeImportErrorCodeExtensions.FromDbValue(dbValue));
        }
    }

    private static string ReadContract(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", fileName));
}
