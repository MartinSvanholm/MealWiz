using System.Globalization;
using MealWiz.Shared.Localization;
using Xunit;

namespace MealWiz.Shared.Tests.Localization.UnitTests;

public class CultureResolverTests
{
    [Theory]
    [InlineData("da-DK", "da")]
    [InlineData("da", "da")]
    [InlineData("de-DE", "en")]
    [InlineData("sv-SE", "en")]
    [InlineData("nb-NO", "en")]
    [InlineData("en-US", "en")]
    [InlineData("en-GB", "en")]
    [InlineData("", "en")]
    public void Resolve_WithoutAStoredChoice_FollowsTheDeviceCulture(string device, string expected)
    {
        var culture = CultureResolver.Resolve(null, CultureInfo.GetCultureInfo(device));

        Assert.Equal(expected, culture.Name);
    }

    [Theory]
    [InlineData("en", "da-DK", "en")]
    [InlineData("da", "en-US", "da")]
    [InlineData("da", "de-DE", "da")]
    [InlineData("DA", "en-US", "da")]
    public void Resolve_AStoredChoice_BeatsTheDeviceCulture(string stored, string device, string expected)
    {
        var culture = CultureResolver.Resolve(stored, CultureInfo.GetCultureInfo(device));

        Assert.Equal(expected, culture.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("fr")]
    [InlineData("da-DK")]
    [InlineData("\"da\"")]
    public void Resolve_AnUnsupportedStoredValue_IsIgnored(string stored)
    {
        Assert.Equal("en", CultureResolver.Resolve(stored, CultureInfo.GetCultureInfo("en-US")).Name);
        Assert.Equal("da", CultureResolver.Resolve(stored, CultureInfo.GetCultureInfo("da-DK")).Name);
    }
}
