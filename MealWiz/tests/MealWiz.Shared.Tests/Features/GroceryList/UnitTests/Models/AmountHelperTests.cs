using MealWiz.Shared.Features.GroceryList.Models;
using Xunit;

namespace MealWiz.Shared.Tests.Features.GroceryList.UnitTests.Models;

public class AmountHelperTests
{
    [Theory]
    [InlineData("200 g", "300 g", "500 g")]
    [InlineData("200g", "300 g", "500 g")]
    [InlineData("400 g", "200g", "600 g")]
    [InlineData("1,5 dl", "1 dl", "2,5 dl")]
    [InlineData("1.5 dl", "1,25 dl", "2.75 dl")]
    [InlineData("1,5 dl", "1,5 dl", "3 dl")]
    [InlineData("1 L", "2 l", "3 L")]
    [InlineData("2", "3", "5")]
    [InlineData("0.1 l", "0.2 l", "0.3 l")]
    [InlineData("2 stk.", "3 stk", "5 stk.")]
    public void SuggestTotal_SameUnit_Sums(string first, string second, string expected) =>
        Assert.Equal(expected, AmountHelper.SuggestTotal([first, second]));

    [Fact]
    public void SuggestTotal_SingleAmount_ReturnsItAsParsed() =>
        Assert.Equal("200 g", AmountHelper.SuggestTotal(["200 g"]));

    [Theory]
    [InlineData("200 g", "0.2 kg")]
    [InlineData("200 g", "2")]
    [InlineData("200 g", "")]
    [InlineData("200 g", null)]
    [InlineData("1/2 tsp", "1 tsp")]
    [InlineData("2-3 dl", "1 dl")]
    [InlineData("1 can (400 g)", "1 can")]
    [InlineData("1.000 g", "500 g")]
    public void SuggestTotal_ReturnsNull(string? first, string? second) =>
        Assert.Null(AmountHelper.SuggestTotal([first, second]));

    [Fact]
    public void SuggestTotal_NoAmounts_ReturnsNull() =>
        Assert.Null(AmountHelper.SuggestTotal([]));

    [Theory]
    [InlineData("200g", 200, "g")]
    [InlineData("1,5 dl", 1.5, "dl")]
    [InlineData("3", 3, "")]
    [InlineData(" 2 stk. ", 2, "stk")]
    public void TryParse_ReadsQuantityAndUnit(string input, double quantity, string unit)
    {
        Assert.True(AmountHelper.TryParse(input, out var parsedQuantity, out var parsedUnit));
        Assert.Equal((decimal)quantity, parsedQuantity);
        Assert.Equal(unit, parsedUnit);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public void TryParse_Unparseable_ReturnsFalse(string? input) =>
        Assert.False(AmountHelper.TryParse(input, out _, out _));

    [Theory]
    [InlineData("700 g", "500 g", "750 g", "950 g")]
    [InlineData("500 g", "500 g", "750 g", "750 g")]
    [InlineData("700g", "500 g", "750 g", "950 g")]
    [InlineData("700 g", "500 g", "200 g", "400 g")]
    [InlineData("30 g", "20 g", "30 g", "40 g")]
    [InlineData("1,5 dl", "1 dl", "2 dl", "2,5 dl")]
    [InlineData("2.5 dl", "1,5 dl", "1 dl", "2 dl")]
    [InlineData("1 L", "1 l", "2 l", "2 L")]
    [InlineData("5", "2", "3", "6")]
    [InlineData("2 stk.", "1 stk", "2 stk", "3 stk.")]
    [InlineData("0.3 l", "0.1 l", "0.2 l", "0.4 l")]
    public void Adjust_SameUnit_ReplacesTheOldAmountWithTheNew(string item, string old, string @new, string expected) =>
        Assert.Equal(expected, AmountHelper.Adjust(item, old, @new));

    [Theory]
    [InlineData("500 g", "500 g", "0 g")]
    [InlineData("300 g", "500 g", "200 g")]
    [InlineData("200 g", "500 g", "100 g")]
    [InlineData("700 g", "500 g", "0.75 kg")]
    [InlineData("700 g", "500 g", "")]
    [InlineData("700 g", "", "750 g")]
    [InlineData(null, "500 g", "750 g")]
    [InlineData("700", "500 g", "750 g")]
    [InlineData("1/2 tsp", "1 tsp", "2 tsp")]
    [InlineData("700 g", "500 g", "2-3 dl")]
    [InlineData("1 pack", "200 g", "250 g")]
    public void Adjust_ReturnsNull(string? item, string? old, string? @new) =>
        Assert.Null(AmountHelper.Adjust(item, old, @new));

    // ---- Subtract (MEA-99) ----

    [Theory]
    [InlineData("3 cloves", "2 cloves", "1 clove")]
    [InlineData("3 cloves", "1 clove", "2 cloves")]
    [InlineData("3 clove", "1 cloves", "2 clove")]
    [InlineData("500 g", "200 g", "300 g")]
    [InlineData("500g", "200 g", "300 g")]
    [InlineData("2,5 dl", "1 dl", "1,5 dl")]
    [InlineData("2.75 dl", "1,25 dl", "1.5 dl")]
    [InlineData("1 L", "0.5 l", "0.5 L")]
    [InlineData("5", "2", "3")]
    [InlineData("5 stk.", "2 stk", "3 stk.")]
    [InlineData("3 Cups", "2 cups", "1 Cup")]
    [InlineData("3 tbs", "2 tbs", "1 tbs")]
    [InlineData("3 glass", "2 glass", "1 glass")]
    public void Subtract_SameUnit_Subtracts(string item, string removed, string expected) =>
        Assert.Equal(expected, AmountHelper.Subtract(item, removed));

    [Theory]
    [InlineData("500 g", "0.5 kg")]
    [InlineData("500 g", "500 g")]
    [InlineData("200 g", "300 g")]
    [InlineData("3", "2 g")]
    [InlineData("3 cloves", "")]
    [InlineData("3 cloves", null)]
    [InlineData(null, "2 cloves")]
    [InlineData("", "1 clove")]
    [InlineData("1/2 tsp", "1 tsp")]
    [InlineData("2-3 dl", "1 dl")]
    [InlineData("3 gs", "1 g")]
    public void Subtract_ReturnsNull(string? item, string? removed) =>
        Assert.Null(AmountHelper.Subtract(item, removed));

    [Fact]
    public void The_plural_tolerance_is_for_Subtract_only() =>
        Assert.Null(AmountHelper.SuggestTotal(["2 cloves", "1 clove"]));
}
