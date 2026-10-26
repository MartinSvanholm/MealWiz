using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using MealWiz.Shared.Tests.TestInfrastructure.Builders;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.IntegrationTests;

/// <summary>
/// Runs the real handler as a freshly signed-in user, so what comes back is what RLS lets that user see.
/// Both tests share the class's user, so neither depends on what the other has inserted.
/// </summary>
[Trait("Category", "Integration")]
public class GetAllMealsTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    [Fact]
    public async Task FreshUser_SeesSeededSystemMealsButNoOtherUsersMeals()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);

        var meals = await GetAllMealsAsync();

        Assert.Contains(meals, m => m is { Name: "Leftovers", Type: MealType.Leftover, CreatedBy: null });
        Assert.Contains(meals, m => m is { Name: "Eat Out", Type: MealType.EatOut, CreatedBy: null });
        Assert.All(meals, m => Assert.True(m.CreatedBy is null || m.CreatedBy == supabase.UserId,
            $"Saw '{m.Name}', owned by {m.CreatedBy}."));
        Assert.DoesNotContain(meals, m => m.Name == "Spaghetti Bolognese");
    }

    [Fact]
    public async Task OwnMeal_IsListedAfterSystemMeals()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var ownMeal = await new MealBuilder().InsertAsync(supabase.Client);

        var meals = await GetAllMealsAsync();

        var ownIndex = meals.FindIndex(m => m.Id == ownMeal.Id);
        Assert.True(ownIndex >= 0, "The user's own meal wasn't returned.");
        Assert.Equal(supabase.UserId, meals[ownIndex].CreatedBy);
        Assert.True(meals.FindLastIndex(m => !m.IsRegular) < ownIndex, "System meals should come before regular meals.");
    }

    private async Task<List<Meal>> GetAllMealsAsync()
    {
        var result = await new GetAllMeals.Handler(supabase.Client)
            .Handle(new GetAllMeals.Query(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Value;
    }
}
