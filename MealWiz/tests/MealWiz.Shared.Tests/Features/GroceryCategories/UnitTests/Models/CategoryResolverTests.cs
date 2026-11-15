using MealWiz.Shared.Features.GroceryCategories.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;
using static MealWiz.Shared.Features.GroceryCategories.Models.DefaultCategoryKey;

namespace MealWiz.Shared.Tests.Features.GroceryCategories.UnitTests.Models;

/// <summary>
/// Places a grocery name in a section from its name alone: the whole name, then each word from last to first, then a
/// word ending in a dictionary word of four or more letters (Danish compounds), else Other. A handful of words per
/// section in each language, not the whole dictionary.
/// </summary>
public class CategoryResolverTests
{
    [Theory]
    [InlineData("Apples", Produce)]
    [InlineData("tomatoes", Produce)]
    [InlineData("gulerødder", Produce)]
    [InlineData("hvidløg", Produce)]
    [InlineData("Bread", Bakery)]
    [InlineData("croissants", Bakery)]
    [InlineData("rugbrød", Bakery)]
    [InlineData("chicken breast", MeatFish)]
    [InlineData("salmon", MeatFish)]
    [InlineData("hakket oksekød", MeatFish)]
    [InlineData("laks", MeatFish)]
    [InlineData("salami", Deli)]
    [InlineData("hummus", Deli)]
    [InlineData("leverpostej", Deli)]
    [InlineData("milk", DairyEggs)]
    [InlineData("mælk", DairyEggs)]
    [InlineData("eggs", DairyEggs)]
    [InlineData("æg", DairyEggs)]
    [InlineData("piskefløde", DairyEggs)]
    [InlineData("ice cream", Frozen)]
    [InlineData("frosne ærter", Frozen)]
    [InlineData("spaghetti", Pantry)]
    [InlineData("olive oil", Pantry)]
    [InlineData("havregryn", Pantry)]
    [InlineData("coconut milk", Pantry)]
    [InlineData("beer", Drinks)]
    [InlineData("kaffe", Drinks)]
    [InlineData("rødvin", Drinks)]
    [InlineData("crisps", Snacks)]
    [InlineData("chokolade", Snacks)]
    [InlineData("toilet paper", Household)]
    [InlineData("opvaskemiddel", Household)]
    [InlineData("shampoo", Household)]
    public void ResolveKey_KnownWords_LandInTheirSection(string name, DefaultCategoryKey expected) =>
        Assert.Equal(expected, CategoryResolver.ResolveKey(name));

    [Theory]
    [InlineData("en-US")]
    [InlineData("da")]
    public void ResolveKey_BothLanguages_WhateverTheAppLanguage(string culture)
    {
        using var _ = CultureScope.Use(culture);

        Assert.Equal(DairyEggs, CategoryResolver.ResolveKey("milk"));
        Assert.Equal(DairyEggs, CategoryResolver.ResolveKey("mælk"));
        Assert.Equal(Bakery, CategoryResolver.ResolveKey("bread"));
        Assert.Equal(Bakery, CategoryResolver.ResolveKey("brød"));
    }

    [Theory]
    [InlineData("MILK")]
    [InlineData("  Mælk  ")]
    [InlineData("Olive   Oil")]
    public void ResolveKey_IgnoresCaseAndSpaces(string name) =>
        Assert.NotEqual(Other, CategoryResolver.ResolveKey(name));

    [Theory]
    [InlineData("hakkede tomater", Produce)]
    [InlineData("organic whole milk", DairyEggs)]
    [InlineData("2 x pasta (penne)", Pantry)]
    [InlineData("salmon, smoked", MeatFish)]
    public void ResolveKey_MatchesTheMainWord_LastWordFirst(string name, DefaultCategoryKey expected) =>
        Assert.Equal(expected, CategoryResolver.ResolveKey(name));

    [Fact]
    public void ResolveKey_WholeNameBeatsItsWords()
    {
        // "milk" alone is dairy; the whole name "coconut milk" is in the dictionary as pantry.
        Assert.Equal(Pantry, CategoryResolver.ResolveKey("coconut milk"));
        Assert.Equal(DairyEggs, CategoryResolver.ResolveKey("milk"));
    }

    [Theory]
    [InlineData("cherrytomater", Produce)]
    [InlineData("økologiskmælk", DairyEggs)]
    [InlineData("æblejuice", Drinks)]
    [InlineData("kartoffelchips", Snacks)]
    public void ResolveKey_DanishCompound_EndsInADictionaryWord(string name, DefaultCategoryKey expected) =>
        Assert.Equal(expected, CategoryResolver.ResolveKey(name));

    [Fact]
    public void ResolveKey_CompoundEnding_NeedsFourLetters()
    {
        // "ost" (cheese) has three letters, so a word that merely ends in it is not cheese.
        Assert.Equal(DairyEggs, CategoryResolver.ResolveKey("ost"));
        Assert.Equal(Other, CategoryResolver.ResolveKey("xyzost"));
        // "mælk" has four.
        Assert.Equal(DairyEggs, CategoryResolver.ResolveKey("xyzmælk"));
    }

    [Fact]
    public void ResolveKey_CompoundEnding_LongestEndingWins()
    {
        // Ends with both "salat" (produce) and "hønsesalat" (deli): the longer ending decides.
        Assert.Equal(Deli, CategoryResolver.ResolveKey("grovhønsesalat"));
    }

    [Theory]
    [InlineData("flimflam")]
    [InlineData("Zorbex 3000")]
    public void ResolveKey_UnknownName_IsOther(string name) =>
        Assert.Equal(Other, CategoryResolver.ResolveKey(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!")]
    public void ResolveKey_EmptyName_IsOther(string? name) =>
        Assert.Equal(Other, CategoryResolver.ResolveKey(name));

    private static List<GroceryCategory> Categories(params DefaultCategoryKey[] keys) =>
        keys.Select((k, i) => new GroceryCategory { Id = 100 + i, DefaultKey = k, Position = i + 1 }).ToList();

    [Fact]
    public void Resolve_ReturnsTheUsersCategoryWithThatKey()
    {
        var categories = Categories(Produce, DairyEggs, Other);

        Assert.Equal(101, CategoryResolver.Resolve("milk", categories)?.Id);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsTheUsersOther()
    {
        var categories = Categories(Produce, DairyEggs, Other);

        Assert.Equal(102, CategoryResolver.Resolve("flimflam", categories)?.Id);
    }

    [Fact]
    public void Resolve_UserHasNoCategoryWithThatKey_FallsBackToOther()
    {
        var categories = Categories(Produce, Other);

        Assert.Equal(101, CategoryResolver.Resolve("milk", categories)?.Id);
    }

    [Fact]
    public void Resolve_NoCategories_ReturnsNull()
    {
        Assert.Null(CategoryResolver.Resolve("milk", []));
    }

    [Fact]
    public void Resolve_ARememberedName_BeatsTheDictionary_IgnoringCaseAndSpaces()
    {
        var categories = Categories(Produce, DairyEggs, Frozen, Other);
        var remembered = new Dictionary<string, int> { ["milk"] = 102, ["tofu"] = 100 };

        Assert.Equal(102, CategoryResolver.Resolve("  MILK ", categories, remembered)?.Id);
        Assert.Equal(100, CategoryResolver.Resolve("Tofu", categories, remembered)?.Id);
    }

    [Fact]
    public void Resolve_ARememberedNameMatchesTheWholeName_NotItsWords()
    {
        var categories = Categories(Produce, DairyEggs, Frozen, Other);
        var remembered = new Dictionary<string, int> { ["milk"] = 102 };

        Assert.Equal(101, CategoryResolver.Resolve("oat milk", categories, remembered)?.Id);
    }

    [Fact]
    public void Resolve_ARememberedCategoryThatIsNotLoaded_IsIgnored()
    {
        var categories = Categories(Produce, DairyEggs, Other);
        var remembered = new Dictionary<string, int> { ["milk"] = 999 };

        Assert.Equal(101, CategoryResolver.Resolve("milk", categories, remembered)?.Id);
    }
}
