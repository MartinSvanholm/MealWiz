using MealWiz.Shared.Localization;
using Microsoft.Extensions.Localization;
using Xunit;

namespace MealWiz.Shared.Tests.Localization.UnitTests;

public class StringLocalizerExtensionsTests
{
    private readonly IStringLocalizer _localizer = new FakeLocalizer(new()
    {
        ["Items_One"] = "{0} item",
        ["Items_Other"] = "{0} items",
    });

    [Theory]
    [InlineData(1, "1 item")]
    [InlineData(0, "0 items")]
    [InlineData(2, "2 items")]
    [InlineData(12, "12 items")]
    public void Plural_PicksTheOneOrOtherKey(int count, string expected)
    {
        Assert.Equal(expected, _localizer.Plural("Items", count));
    }

    private sealed class FakeLocalizer(Dictionary<string, string> values) : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, values.GetValueOrDefault(name, name), !values.ContainsKey(name));

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(values.GetValueOrDefault(name, name), arguments), !values.ContainsKey(name));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            values.Select(v => new LocalizedString(v.Key, v.Value));
    }
}
