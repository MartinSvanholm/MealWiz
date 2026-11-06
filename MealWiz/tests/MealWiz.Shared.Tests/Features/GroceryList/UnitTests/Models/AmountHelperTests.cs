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
}
