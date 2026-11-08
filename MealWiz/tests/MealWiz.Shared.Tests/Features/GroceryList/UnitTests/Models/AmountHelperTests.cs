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
}
