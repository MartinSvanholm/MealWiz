using MealWiz.Shared.Helpers;
using Xunit;

namespace MealWiz.Shared.Tests.Helpers.UnitTests;

public class DictionaryExtensionsTests
{
    [Fact]
    public void Add_NewKey_CreatesAListWithTheValue()
    {
        var dictionary = new Dictionary<string, List<int>>();

        dictionary.Add("a", 1);

        Assert.Equal(new[] { 1 }, dictionary["a"]);
    }

    [Fact]
    public void Add_ExistingKey_AppendsToItsList()
    {
        var existing = new List<int> { 1 };
        var dictionary = new Dictionary<string, List<int>> { ["a"] = existing };

        dictionary.Add("a", 2);

        Assert.Same(existing, dictionary["a"]);
        Assert.Equal(new[] { 1, 2 }, existing);
    }

    [Fact]
    public void GetFirstAndRemove_Empty_ReturnsNull()
    {
        var dictionary = new Dictionary<string, int>();

        Assert.Null(dictionary.GetFirstAndRemove());
    }

    [Fact]
    public void GetFirstAndRemove_ReturnsTheFirstPairAndRemovesIt()
    {
        var dictionary = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 };

        var first = dictionary.GetFirstAndRemove();

        Assert.Equal(new KeyValuePair<string, int>("a", 1), first);
        Assert.Equal(new[] { "b" }, dictionary.Keys);
    }
}
