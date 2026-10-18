using MealWiz.Functions.Shared.Ingredients;
using Xunit;

namespace MealWiz.Functions.Tests.Shared.UnitTests.Ingredients;

public class IngredientLineParserTests
{
    private readonly IngredientLineParser _parser = new();

    [Theory]
    // Lines from a real Danish recipe import.
    [InlineData("15 g gær", "15 g", "gær")]
    [InlineData("2½ dl sødmælk, stuetempereret", "2½ dl", "sødmælk, stuetempereret")]
    [InlineData("2 dl kærnemælk, stuetempereret", "2 dl", "kærnemælk, stuetempereret")]
    [InlineData("1½ spsk sukker", "1½ spsk", "sukker")]
    [InlineData("1 tsk stødt kardemomme", "1 tsk", "stødt kardemomme")]
    // Numbers: decimals, fractions, mixed numbers, ranges, unit glued to the number.
    [InlineData("1,5 kg kartofler", "1,5 kg", "kartofler")]
    [InlineData("1.5 kg potatoes", "1.5 kg", "potatoes")]
    [InlineData("1/2 tsp salt", "1/2 tsp", "salt")]
    [InlineData("½ tsk salt", "½ tsk", "salt")]
    [InlineData("1 1/2 cups flour", "1 1/2 cups", "flour")]
    [InlineData("2 ½ dl fløde", "2 ½ dl", "fløde")]
    [InlineData("2-3 fed hvidløg", "2-3 fed", "hvidløg")]
    [InlineData("2–3 cloves garlic", "2–3 cloves", "garlic")]
    [InlineData("2 til 3 spsk olie", "2 til 3 spsk", "olie")]
    [InlineData("1 to 2 cups water", "1 to 2 cups", "water")]
    [InlineData("100g plain flour", "100g", "plain flour")]
    [InlineData("300ml milk", "300ml", "milk")]
    [InlineData("250 G Smør", "250 G", "Smør")]
    // Unit variants: trailing period, "of" connector, count with no unit.
    [InlineData("2 tbsp. olive oil", "2 tbsp.", "olive oil")]
    [InlineData("1 tsk. salt", "1 tsk.", "salt")]
    [InlineData("2 cups of flour", "2 cups", "flour")]
    [InlineData("2 large eggs", "2", "large eggs")]
    [InlineData("2 løg", "2", "løg")]
    [InlineData("2 gulerødder", "2", "gulerødder")]
    // Parenthetical size belongs to the amount.
    [InlineData("1 (400 g) dåse hakkede tomater", "1 (400 g) dåse", "hakkede tomater")]
    [InlineData("2 (14 oz) cans chickpeas", "2 (14 oz) cans", "chickpeas")]
    // Approximation words stay in the amount.
    [InlineData("ca. 2 dl mælk", "ca. 2 dl", "mælk")]
    [InlineData("cirka 300 g oksekød", "cirka 300 g", "oksekød")]
    [InlineData("about 2 cups water", "about 2 cups", "water")]
    // Number words are converted to digits.
    [InlineData("en knivspids salt", "1 knivspids", "salt")]
    [InlineData("a pinch of salt", "1 pinch", "salt")]
    [InlineData("et æble", "1", "æble")]
    [InlineData("an onion", "1", "onion")]
    [InlineData("to store æg", "2", "store æg")]
    [InlineData("three cloves garlic", "3 cloves", "garlic")]
    [InlineData("Tolv mandler", "12", "mandler")]
    [InlineData("halvanden dl fløde", "1½ dl", "fløde")]
    [InlineData("en halv citron", "½", "citron")]
    [InlineData("half a lemon", "½", "lemon")]
    [InlineData("to og en halv dl mælk", "2½ dl", "mælk")]
    [InlineData("one and a half cups sugar", "1½ cups", "sugar")]
    [InlineData("ca. en halv liter vand", "ca. ½ liter", "vand")]
    // Surrounding whitespace is ignored.
    [InlineData("  15 g gær  ", "15 g", "gær")]
    public void Parse_SplitsLeadingAmount(string line, string expectedAmount, string expectedName)
    {
        var result = _parser.Parse(line);

        Assert.Equal(expectedAmount, result.Amount);
        Assert.Equal(expectedName, result.Name);
    }

    [Theory]
    [InlineData("salt")]
    [InlineData("Salt og peber")]
    [InlineData("lemon wedges to serve (optional)")]
    // Articles meaning "some" rather than "one".
    [InlineData("a little olive oil")]
    [InlineData("a few sprigs of thyme")]
    [InlineData("et par æg")]
    [InlineData("en anelse chili")]
    // A percentage isn't a quantity.
    [InlineData("38 % piskefløde")]
    [InlineData("18% fløde")]
    // Nothing would be left for the name.
    [InlineData("2 dl")]
    // A number glued to a non-unit word.
    [InlineData("7up")]
    public void Parse_LeavesLineWholeWhenThereIsNoClearAmount(string line)
    {
        var result = _parser.Parse(line);

        Assert.Equal(string.Empty, result.Amount);
        Assert.Equal(line.Trim(), result.Name);
    }

    [Fact]
    public void Parse_PercentageAfterTheAmountStaysInTheName()
    {
        var result = _parser.Parse("2 dl 38 % piskefløde");

        Assert.Equal("2 dl", result.Amount);
        Assert.Equal("38 % piskefløde", result.Name);
    }
}
