using MealWiz.Shared.Amounts;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Amounts.UnitTests;

public class AmountTextTests
{
    [Theory]
    [InlineData("en-US", 1.5, "dl", "1.5 dl")]
    [InlineData("da", 1.5, "dl", "1,5 dl")]
    [InlineData("en-US", 2, null, "2")]
    [InlineData("da", 2, null, "2")]
    [InlineData("en-US", 1, "tsk", "1 tsp")]
    [InlineData("da", 1, "tsk", "1 tsk")]
    [InlineData("en-US", 3, "spsk", "3 tbsp")]
    [InlineData("da", 3, "spsk", "3 spsk")]
    [InlineData("en-US", 1, "cup", "1 cup")]
    [InlineData("da", 1, "cup", "1 kop")]
    [InlineData("en-US", 1234.5, "g", "1234.5 g")]
    [InlineData("da", 1234.5, "g", "1234,5 g")]
    [InlineData("en-US", 0.333, "kg", "0.33 kg")]
    public void Format_UsesTheCulture(string culture, double quantity, string? unit, string expected)
    {
        using var _ = CultureScope.Use(culture);

        Assert.Equal(expected, AmountText.Format((decimal)quantity, AmountUnit.FromCode(unit)));
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("da")]
    public void Format_RoundTripsThroughParse_ForEveryUnit(string culture)
    {
        using var _ = CultureScope.Use(culture);

        foreach (var unit in AmountUnit.All.Cast<AmountUnit?>().Append(null))
        foreach (var quantity in new[] { 1m, 2.5m, 0.25m, 1234.5m })
        {
            var parsed = ParsedAmount.Parse(AmountText.Format(quantity, unit));

            Assert.Equal(quantity, parsed.Quantity);
            Assert.Equal(unit?.Code, parsed.UnitCode);
        }
    }

    [Theory]
    [InlineData("en-US", "200 g", "200 · gram")]
    [InlineData("da", "200 g", "200 · gram")]
    [InlineData("en-US", "1,5 dl", "1.5 · deciliter")]
    [InlineData("da", "1,5 dl", "1,5 · deciliter")]
    [InlineData("en-US", "2", "2 · pieces")]
    [InlineData("da", "2", "2 · stk")]
    [InlineData("en-US", "2½ tbsp", "2.5 · tablespoon")]
    [InlineData("da", "1 pound", "1 · pund")]
    [InlineData("en-US", "a pinch", "Saved as written")]
    [InlineData("da", "a pinch", "Gemmes som skrevet")]
    [InlineData("en-US", "1-2", "Saved as written")]
    public void Describe_SaysWhatTheAppUnderstood(string culture, string text, string expected)
    {
        using var _ = CultureScope.Use(culture);

        Assert.Equal(expected, AmountText.Describe(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Describe_IsNullForAnEmptyField(string? text) => Assert.Null(AmountText.Describe(text));

    [Fact]
    public void EveryUnit_HasAShortAndAFullName_InBothLanguages()
    {
        foreach (var culture in new[] { "en-US", "da" })
        {
            using var _ = CultureScope.Use(culture);
            foreach (var unit in AmountUnit.All)
            {
                Assert.NotEqual($"Unit_{unit.Code}_Short", MealWiz.Shared.Localization.SharedTexts.Get($"Unit_{unit.Code}_Short"));
                Assert.NotEqual($"Unit_{unit.Code}_Name", MealWiz.Shared.Localization.SharedTexts.Get($"Unit_{unit.Code}_Name"));
            }
        }
    }
}
