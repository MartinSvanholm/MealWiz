using Bunit;
using MealWiz.Shared.Features.MealPlans.Components;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealPlans.ComponentTests;

/// <summary>The "Also remove N items from the grocery list" row shared by the remove and swap drawers.</summary>
public class GroceryItemsSwitchTests : ComponentTestBase
{
    [Theory]
    [InlineData(1, "Also remove 1 item from the grocery list")]
    [InlineData(3, "Also remove 3 items from the grocery list")]
    public void Labels_the_count_and_uses_the_prefix_for_its_test_ids(int count, string label)
    {
        var cut = Render<GroceryItemsSwitch>(p => p.Add(x => x.Count, count).Add(x => x.TestIdPrefix, "x").Add(x => x.Value, true));

        Assert.NotNull(cut.Find(TestId("x-grocery-row")));
        Assert.Equal(label, cut.Find(TestId("x-grocery-label")).TextContent);
        Assert.Equal(label, cut.Find(InputOf("x-grocery-switch")).GetAttribute("aria-label"));
        Assert.True(cut.Find(InputOf("x-grocery-switch")).HasAttribute("checked"));
    }

    [Fact]
    public void Toggling_raises_ValueChanged()
    {
        bool? changed = null;
        var cut = Render<GroceryItemsSwitch>(p => p
            .Add(x => x.Count, 2).Add(x => x.TestIdPrefix, "x").Add(x => x.Value, true)
            .Add(x => x.ValueChanged, v => changed = v));

        cut.Find(InputOf("x-grocery-switch")).Change(false);

        Assert.False(changed);
    }
}

/// <summary>The row's label comes from a resource (UI-03): every localizer echoes its key as <c>[Key]</c>.</summary>
public class GroceryItemsSwitchTextTests() : ComponentTestBase(echoResourceKeys: true)
{
    [Fact]
    public void The_label_reads_from_the_resource()
    {
        var cut = Render<GroceryItemsSwitch>(p => p.Add(x => x.Count, 2).Add(x => x.TestIdPrefix, "x"));

        Assert.Equal("[GroceryItems_AlsoRemove_Other]", cut.Find(TestId("x-grocery-label")).TextContent);
    }
}
