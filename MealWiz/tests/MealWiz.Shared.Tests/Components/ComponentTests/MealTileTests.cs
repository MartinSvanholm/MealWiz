using Bunit;
using MealWiz.Shared.Components;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Components.ComponentTests;

public class MealTileTests : ComponentTestBase
{
    [Theory]
    [InlineData("lasagne", "L")]
    [InlineData("  Æbleskiver", "Æ")]
    [InlineData("", "?")]
    public void Shows_the_meals_initial_in_upper_case(string name, string expected)
    {
        var cut = Render<MealTile>(p => p.Add(t => t.Name, name));

        Assert.Equal(expected, cut.Find(TestId("meal-tile-initial")).TextContent);
    }

    [Theory]
    [InlineData(0, "orange")]
    [InlineData(1, "green")]
    [InlineData(2, "orange")]
    [InlineData(5, "green")]
    public void Colour_alternates_orange_and_green_by_position(int index, string expected)
    {
        var cut = Render<MealTile>(p => p.Add(t => t.Name, "Tacos").Add(t => t.Index, index));

        Assert.Equal(expected, cut.Find(TestId("meal-tile")).GetAttribute("data-color"));
    }

    [Fact]
    public void Neutral_tile_ignores_the_position()
    {
        var cut = Render<MealTile>(p => p.Add(t => t.Name, "Eat Out").Add(t => t.Index, 1).Add(t => t.Neutral, true));

        Assert.Equal("neutral", cut.Find(TestId("meal-tile")).GetAttribute("data-color"));
    }

    [Fact]
    public void Day_tag_shows_only_when_set()
    {
        var plain = Render<MealTile>(p => p.Add(t => t.Name, "Tacos"));
        var tagged = Render<MealTile>(p => p.Add(t => t.Name, "Tacos").Add(t => t.DayTag, "MON"));

        Assert.Empty(plain.FindAll(TestId("meal-tile-day")));
        Assert.Equal("MON", tagged.Find(TestId("meal-tile-day")).TextContent);
    }
}
