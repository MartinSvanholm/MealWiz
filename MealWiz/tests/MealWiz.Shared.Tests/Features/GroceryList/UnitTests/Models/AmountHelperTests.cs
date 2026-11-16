using MealWiz.Shared.Features.GroceryList.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.UnitTests.Models;

public class AmountHelperTests
{
    // ---- SuggestTotal ----

    [Theory]
    [InlineData("200 g", "300 g", "500 g")]
    [InlineData("200g", "300 g", "500 g")]
    [InlineData("200 g", "0,5 kg", "700 g")]
    [InlineData("1 kg", "200 g", "1.2 kg")]
    [InlineData("1 kg", "234 g", "1234 g")]
    [InlineData("500 g", "0.5 kg", "1 kg")]
    [InlineData("5 dl", "250 ml", "7.5 dl")]
    [InlineData("5 dl", "5 dl", "1 l")]
    [InlineData("1 L", "2 l", "3 l")]
    [InlineData("1,5 dl", "1 dl", "2.5 dl")]
    [InlineData("8 oz", "1 lb", "1.5 lb")]
    [InlineData("1 lb", "1 oz", "17 oz")]
    [InlineData("1 oz", "100 g", "128.35 g")]
    [InlineData("2", "3", "5")]
    [InlineData("2 stk.", "3", "5")]
    [InlineData("2 spsk", "1 tbsp", "3 tbsp")]
    [InlineData("1 cup", "2 kopper", "3 cup")]
    [InlineData("½ dl", "2½ dl", "3 dl")]
    [InlineData("1/2 tsp", "1 tsk", "1.5 tsp")]
    [InlineData("3 cloves", "2 Cloves.", "5 cloves")]
    [InlineData("2 cloves", "1 clove", "3 cloves")]
    [InlineData("1 bunch", "1 bunch", "2 bunch")]
    public void SuggestTotal_InOneFamilyOrSameWord_Sums(string first, string second, string expected)
    {
        using var _ = CultureScope.Use("en-US");

        Assert.Equal(expected, AmountHelper.SuggestTotal([first, second]));
    }

    [Theory]
    [InlineData("200 g", "0,5 kg", "700 g")]
    [InlineData("1 kg", "200 g", "1,2 kg")]
    [InlineData("5 dl", "250 ml", "7,5 dl")]
    [InlineData("2 spsk", "1 tbsp", "3 spsk")]
    [InlineData("1 cup", "1 cup", "2 kop")]
    [InlineData("1,5 fed", "1 fed", "2,5 fed")]
    public void SuggestTotal_InDanish_UsesTheDecimalCommaAndDanishShortNames(string first, string second, string expected)
    {
        using var _ = CultureScope.Use("da");

        Assert.Equal(expected, AmountHelper.SuggestTotal([first, second]));
    }

    [Theory]
    [InlineData(new[] { "1 kg", "500 g", "0.5 lb" }, "1726.8 g")]
    [InlineData(new[] { "1 kg", "200 g", "1 oz" }, "1228.35 g")]
    [InlineData(new[] { "1 kg", "500 g", "500 g" }, "2 kg")]
    public void SuggestTotal_AddsManyAmountsAcrossTheFamily(string[] amounts, string expected)
    {
        using var _ = CultureScope.Use("en-US");

        Assert.Equal(expected, AmountHelper.SuggestTotal(amounts));
    }

    [Fact]
    public void SuggestTotal_SingleAmount_ReturnsItInTheBestUnit()
    {
        using var _ = CultureScope.Use("en-US");

        Assert.Equal("200 g", AmountHelper.SuggestTotal(["200 g"]));
    }

    [Theory]
    [InlineData("200 g", "1 dl")]
    [InlineData("200 g", "2")]
    [InlineData("1 cup", "1 tsk")]
    [InlineData("1 tsk", "1 spsk")]
    [InlineData("a pinch", "2 g")]
    [InlineData("2 cloves", "1 g")]
    [InlineData("2 cloves", "1 bunch")]
    [InlineData("200 g", "")]
    [InlineData("200 g", null)]
    [InlineData("2-3 dl", "1 dl")]
    [InlineData("1 can (400 g)", "1 can")]
    [InlineData("1.000 g", "500 g")]
    public void SuggestTotal_ReturnsNull(string? first, string? second) =>
        Assert.Null(AmountHelper.SuggestTotal([first, second]));

    [Fact]
    public void SuggestTotal_NoAmounts_ReturnsNull() =>
        Assert.Null(AmountHelper.SuggestTotal([]));

    // ---- Adjust ----

    [Theory]
    [InlineData("700 g", "500 g", "750 g", "950 g")]
    [InlineData("700 g", "500 g", "0,75 kg", "950 g")]
    [InlineData("1,2 kg", "200 g", "0,5 kg", "1.5 kg")]
    [InlineData("500 g", "500 g", "750 g", "750 g")]
    [InlineData("1,5 dl", "1 dl", "2 dl", "2.5 dl")]
    [InlineData("1 L", "1 l", "2 l", "2 l")]
    [InlineData("5", "2", "3", "6")]
    [InlineData("2 stk.", "1 stk", "2 stk", "3")]
    [InlineData("3 cloves", "2 cloves", "1 clove", "2 cloves")]
    [InlineData("2 bunch", "1 bunch", "2 bunch", "3 bunch")]
    public void Adjust_ReplacesTheOldAmountWithTheNew(string item, string old, string @new, string expected)
    {
        using var _ = CultureScope.Use("en-US");

        Assert.Equal(expected, AmountHelper.Adjust(item, old, @new));
    }

    [Theory]
    [InlineData("500 g", "500 g", "0 g")]
    [InlineData("300 g", "500 g", "200 g")]
    [InlineData("200 g", "0,5 kg", "0,1 kg")]
    [InlineData("700 g", "500 g", "1 dl")]
    [InlineData("700 g", "500 g", "")]
    [InlineData("700 g", "", "750 g")]
    [InlineData(null, "500 g", "750 g")]
    [InlineData("700", "500 g", "750 g")]
    [InlineData("700 g", "500 g", "2-3 dl")]
    [InlineData("1 pack", "200 g", "250 g")]
    public void Adjust_ReturnsNull(string? item, string? old, string? @new) =>
        Assert.Null(AmountHelper.Adjust(item, old, @new));

    // ---- Subtract (MEA-99) ----

    [Theory]
    [InlineData("700 g", "0,5 kg", "200 g")]
    [InlineData("1,2 kg", "200 g", "1 kg")]
    [InlineData("1 l", "2,5 dl", "7.5 dl")]
    [InlineData("500 g", "200 g", "300 g")]
    [InlineData("2,5 dl", "1 dl", "1.5 dl")]
    [InlineData("5", "2", "3")]
    [InlineData("5 stk.", "2 stk", "3")]
    [InlineData("3 Cups", "2 cups", "1 cup")]
    [InlineData("3 tbs", "2 tbs", "1 tbsp")]
    [InlineData("3 cloves", "2 cloves", "1 clove")]
    [InlineData("3 cloves", "1 clove", "2 cloves")]
    [InlineData("3 clove", "1 cloves", "2 clove")]
    [InlineData("3 glass", "2 glass", "1 glass")]
    public void Subtract_InOneFamilyOrSameWord_Subtracts(string item, string removed, string expected)
    {
        using var _ = CultureScope.Use("en-US");

        Assert.Equal(expected, AmountHelper.Subtract(item, removed));
    }

    [Theory]
    [InlineData("700 g", "0,5 kg", "200 g")]
    [InlineData("1 l", "2,5 dl", "7,5 dl")]
    [InlineData("3 spsk", "1 tbsp", "2 spsk")]
    public void Subtract_InDanish_UsesTheDecimalComma(string item, string removed, string expected)
    {
        using var _ = CultureScope.Use("da");

        Assert.Equal(expected, AmountHelper.Subtract(item, removed));
    }

    [Theory]
    [InlineData("500 g", "1 kg")]
    [InlineData("500 g", "0,5 kg")]
    [InlineData("200 g", "300 g")]
    [InlineData("2 dl", "1 g")]
    [InlineData("3", "2 g")]
    [InlineData("3 cloves", "")]
    [InlineData("3 cloves", null)]
    [InlineData(null, "2 cloves")]
    [InlineData("", "1 clove")]
    [InlineData("2-3 dl", "1 dl")]
    [InlineData("3 gs", "1 g")]
    public void Subtract_ReturnsNull(string? item, string? removed) =>
        Assert.Null(AmountHelper.Subtract(item, removed));
}
