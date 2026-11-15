using System.Text.Json;
using MealWiz.Shared.Amounts;
using Xunit;

namespace MealWiz.Shared.Tests.Amounts.UnitTests;

/// <summary>The C# parser against <c>amount_parsing.json</c>, which the pgTAP test pins <c>parse_amount</c> to as well.</summary>
public class ParsedAmountTests
{
    public record ContractCase(string Text, decimal? Quantity, string? Unit);

    private static readonly JsonElement Contract = JsonDocument.Parse(
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "amount_parsing.json"))).RootElement;

    public static IEnumerable<TheoryDataRow<ContractCase>> Cases() =>
        Contract.GetProperty("cases").EnumerateArray().Select(c => new TheoryDataRow<ContractCase>(new ContractCase(
            c.GetProperty("text").GetString()!,
            c.GetProperty("quantity").ValueKind == JsonValueKind.Null ? null : c.GetProperty("quantity").GetDecimal(),
            c.GetProperty("unit").GetString())));

    [Theory]
    [MemberData(nameof(Cases))]
    public void Parse_AgreesWithTheContract(ContractCase expected)
    {
        var parsed = ParsedAmount.Parse(expected.Text);

        Assert.Equal(expected.Quantity, parsed.Quantity);
        Assert.Equal(expected.Unit, parsed.UnitCode);
        Assert.Equal(expected.Text, parsed.Text);
    }

    [Fact]
    public void PgTapTest_EmbedsTheSameContract()
    {
        var sql = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contracts", "amounts.test.sql"));
        var begin = sql.IndexOf("-- contract:begin", StringComparison.Ordinal);
        var end = sql.IndexOf("-- contract:end", StringComparison.Ordinal);
        var block = sql[begin..end];
        const string quote = "$contract$";
        var from = block.IndexOf(quote, StringComparison.Ordinal) + quote.Length;
        var to = block.LastIndexOf(quote, StringComparison.Ordinal);

        var embedded = JsonDocument.Parse(block[from..to]).RootElement;

        Assert.True(JsonSerializer.Serialize(Normalize(embedded)) == JsonSerializer.Serialize(Normalize(Contract)),
            "supabase/tests/database/amounts.test.sql embeds a different contract than MealWiz/tests/Contracts/amount_parsing.json");
    }

    // Numbers compare by value, so 2 and 2.0 are the same.
    private static object? Normalize(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => Normalize(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Select(Normalize).ToList(),
        JsonValueKind.Number => e.GetDecimal(),
        JsonValueKind.String => e.GetString(),
        _ => null
    };

    [Fact]
    public void Contract_ListsTheTenUnitsInOrder() =>
        Assert.Equal(AmountUnit.All.Select(u => u.Code),
            Contract.GetProperty("units").EnumerateArray().Select(u => u.GetString()));

    [Fact]
    public void Contract_CoversEverySpellingOfEveryUnit()
    {
        var texts = Contract.GetProperty("cases").EnumerateArray().Select(c => c.GetProperty("text").GetString()).ToHashSet();
        foreach (var word in new[] { "gr", "kilos", "lbs", "millilitres", "decilitre", "litres", "kopper", "teskefulde", "tsps", "tbs", "spiseskefulde", "pcs", "styks" })
            Assert.Contains($"2 {word}", texts);
    }

    [Fact]
    public void Parse_Null_IsEmptyAndNotUnderstood()
    {
        var parsed = ParsedAmount.Parse(null);

        Assert.Equal("", parsed.Text);
        Assert.False(parsed.IsUnderstood);
        Assert.Null(parsed.Family);
    }

    [Theory]
    [InlineData("200 g", UnitFamily.Mass)]
    [InlineData("1 lb", UnitFamily.Mass)]
    [InlineData("2 dl", UnitFamily.Volume)]
    [InlineData("1 cup", UnitFamily.Cup)]
    [InlineData("1 tsk", UnitFamily.Teaspoon)]
    [InlineData("1 tbsp", UnitFamily.Tablespoon)]
    [InlineData("2", UnitFamily.Count)]
    [InlineData("2 stk", UnitFamily.Count)]
    public void Family_FollowsTheUnit(string text, UnitFamily family) => Assert.Equal(family, ParsedAmount.Parse(text).Family);

    [Fact]
    public void Family_IsNullWhenNotUnderstood() => Assert.Null(ParsedAmount.Parse("a pinch").Family);

    [Fact]
    public void Units_HaveTheExactFactors()
    {
        var factors = AmountUnit.All.ToDictionary(u => u.Code, u => u.Factor);

        Assert.Equal(1m, factors["g"]);
        Assert.Equal(1000m, factors["kg"]);
        Assert.Equal(28.349523125m, factors["oz"]);
        Assert.Equal(453.59237m, factors["lb"]);
        Assert.Equal(1m, factors["ml"]);
        Assert.Equal(100m, factors["dl"]);
        Assert.Equal(1000m, factors["l"]);
    }

    [Fact]
    public void CupAndSpoons_AreEachAloneInTheirFamily()
    {
        Assert.Single(AmountUnit.InFamily(UnitFamily.Cup));
        Assert.Single(AmountUnit.InFamily(UnitFamily.Teaspoon));
        Assert.Single(AmountUnit.InFamily(UnitFamily.Tablespoon));
        Assert.Equal(4, AmountUnit.InFamily(UnitFamily.Mass).Count());
        Assert.Equal(3, AmountUnit.InFamily(UnitFamily.Volume).Count());
        Assert.All(AmountUnit.InFamily(UnitFamily.Cup).Concat(AmountUnit.InFamily(UnitFamily.Teaspoon))
            .Concat(AmountUnit.InFamily(UnitFamily.Tablespoon)), u => Assert.Equal(1m, u.Factor));
    }

    [Fact]
    public void FromCode_FindsEveryUnit_AndNothingElse()
    {
        Assert.All(AmountUnit.All, u => Assert.Same(u, AmountUnit.FromCode(u.Code)));
        Assert.Null(AmountUnit.FromCode("stk"));
        Assert.Null(AmountUnit.FromCode(null));
    }
}
