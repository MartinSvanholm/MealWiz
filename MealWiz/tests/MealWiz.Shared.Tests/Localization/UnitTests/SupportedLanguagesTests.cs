using MealWiz.Shared.Localization;
using Xunit;

namespace MealWiz.Shared.Tests.Localization.UnitTests;

public class SupportedLanguagesTests
{
    [Fact]
    public void All_IsEnglishThenDanish_EachInItsOwnName()
    {
        Assert.Equal(
            [new SupportedLanguage("en", "English"), new SupportedLanguage("da", "Dansk")],
            SupportedLanguages.All);
    }

    [Fact]
    public void DefaultCode_IsEnglish()
    {
        Assert.Equal("en", SupportedLanguages.DefaultCode);
    }

    [Fact]
    public void StorageKey_IsTheDocumentedKey()
    {
        // Read by both index.html files and written by the Account page (MEA-106): don't change it.
        Assert.Equal("mealwiz.language", SupportedLanguages.StorageKey);
    }

    [Theory]
    [InlineData("en", true)]
    [InlineData("da", true)]
    [InlineData("DA", true)]
    [InlineData("da-DK", false)]
    [InlineData("fr", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupported_MatchesTheCodes(string? code, bool expected)
    {
        Assert.Equal(expected, SupportedLanguages.IsSupported(code));
    }
}
