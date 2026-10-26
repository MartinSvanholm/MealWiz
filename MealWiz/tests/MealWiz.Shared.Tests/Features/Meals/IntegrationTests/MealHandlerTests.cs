using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Meals.DeleteMeal;
using MealWiz.Shared.Features.Meals.GetAllMeals;
using MealWiz.Shared.Features.Meals.Models;
using MealWiz.Shared.Features.Meals.SaveMeal;
using MealWiz.Shared.Tests.TestInfrastructure;
using MealWiz.Shared.Tests.TestInfrastructure.Builders;
using Supabase;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Meals.IntegrationTests;

/// <summary>
/// Runs the Meals handlers as a freshly signed-in user, so what they read and write is what RLS allows that user.
/// The tests share the class's user, so each one only looks at the rows it created.
/// </summary>
[Trait("Category", "Integration")]
public class MealHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveMeal_NewMeal_InsertsItOwnedByTheUser()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var before = DateTime.UtcNow;
        var meal = new MealBuilder().WithName("Lasagne").WithRecipe("Layer.\nBake.").Build();

        var saved = ResultAssert.Ok(await SaveAsync(meal));

        Assert.NotEqual(0, saved.Id);
        var row = await GetMealRowAsync(supabase.Client, saved.Id);
        Assert.NotNull(row);
        Assert.Equal("Lasagne", row.Name);
        Assert.Equal("Layer.\nBake.", row.Recipe);
        Assert.Equal(MealTypeExtensions.RegularValue, row.MealType);
        Assert.Equal(supabase.UserId, row.CreatedBy);
        TimeAssert.CloseTo(before, row.CreatedAt);
        // Pins current behaviour, see MEA-40: UpdatedAt isn't nullable, so a new meal is stored with 0001-01-01.
        Assert.Equal(1, row.UpdatedAt.Year);
    }

    [Fact]
    public async Task SaveMeal_ExistingMeal_UpdatesItAndSetsUpdatedAt()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().WithName("Soup").InsertAsync(supabase.Client);
        var before = DateTime.UtcNow;

        // What the Edit Meal page does: change the loaded meal and save it.
        meal.Name = "Tomato soup";
        meal.Recipe = "Blend.";
        var saved = ResultAssert.Ok(await SaveAsync(meal));

        Assert.Equal(meal.Id, saved.Id);
        var row = await GetMealRowAsync(supabase.Client, meal.Id);
        Assert.NotNull(row);
        Assert.Equal("Tomato soup", row.Name);
        Assert.Equal("Blend.", row.Recipe);
        Assert.Equal(supabase.UserId, row.CreatedBy);
        TimeAssert.CloseTo(before, row.UpdatedAt);
    }

    [Fact]
    public async Task SaveMeal_AnotherUsersMeal_ReturnsAnEmptyMealAndLeavesItUnchanged()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var otherUser = await supabase.GetOtherUserAsync();
        var othersMeal = await new MealBuilder().WithName("Theirs").InsertAsync(otherUser.Client);

        othersMeal.Name = "Mine now";
        var result = await SaveAsync(othersMeal);

        // RLS filters the row out, so the update matches nothing. The handler reports that as success with an
        // empty meal (id 0) rather than a failure.
        Assert.Equal(0, ResultAssert.Ok(result).Id);
        var row = await GetMealRowAsync(otherUser.Client, othersMeal.Id);
        Assert.Equal("Theirs", row?.Name);
    }

    [Fact]
    public async Task GetAllMeals_FreshUser_SeesSeededSystemMealsButNoOtherUsersMeals()
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
    public async Task GetAllMeals_OwnMeal_IsListedAfterSystemMeals()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var ownMeal = await new MealBuilder().InsertAsync(supabase.Client);

        var meals = await GetAllMealsAsync();

        var ownIndex = meals.FindIndex(m => m.Id == ownMeal.Id);
        Assert.True(ownIndex >= 0, "The user's own meal wasn't returned.");
        Assert.Equal(supabase.UserId, meals[ownIndex].CreatedBy);
        Assert.True(meals.FindLastIndex(m => !m.IsRegular) < ownIndex, "System meals should come before regular meals.");
    }

    [Fact]
    public async Task DeleteMeal_RemovesTheMealAndItsIngredients()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().WithIngredient().WithIngredient().InsertAsync(supabase.Client);

        var result = await new DeleteMeal.Handler(supabase.Client).Handle(new DeleteMeal.Command(meal.Id), CancellationToken);

        ResultAssert.Ok(result);
        Assert.Null(await GetMealRowAsync(supabase.Client, meal.Id));
        var ingredients = await supabase.Client.From<IngredientDb>().Where(x => x.MealId == meal.Id).Get(CancellationToken);
        Assert.Empty(ingredients.Models);
    }

    [Fact]
    public async Task DeleteMeal_PlannedMeal_FailsAndKeepsTheMeal()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);
        var date = new DateTime(2031, 3, 5);
        await new MealPlanBuilder().ForWeekOf(date).WithMeal(date, meal).InsertAsync(supabase.Client);

        var result = await new DeleteMeal.Handler(supabase.Client).Handle(new DeleteMeal.Command(meal.Id), CancellationToken);

        // Pins current behaviour, see MEA-39: meal_plans-meals.fk_meal has no ON DELETE action, so a planned meal
        // can't be deleted. The FK violation comes back as a failed result, not an exception.
        ResultAssert.Failed(result);
        Assert.NotNull(await GetMealRowAsync(supabase.Client, meal.Id));
    }

    [Fact]
    public async Task DeleteMeal_AnotherUsersMeal_SucceedsWithoutDeletingIt()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var otherUser = await supabase.GetOtherUserAsync();
        var othersMeal = await new MealBuilder().InsertAsync(otherUser.Client);

        var result = await new DeleteMeal.Handler(supabase.Client).Handle(new DeleteMeal.Command(othersMeal.Id), CancellationToken);

        // The delete matches no rows the user can see, which isn't an error.
        ResultAssert.Ok(result);
        Assert.NotNull(await GetMealRowAsync(otherUser.Client, othersMeal.Id));
    }

    private Task<FluentResults.Result<Meal>> SaveAsync(Meal meal) =>
        new SaveMeal.Handler(supabase.Client).Handle(new SaveMeal.Command(meal), CancellationToken);

    private async Task<List<Meal>> GetAllMealsAsync() =>
        ResultAssert.Ok(await new GetAllMeals.Handler(supabase.Client).Handle(new GetAllMeals.Query(), CancellationToken));

    private static async Task<MealDb?> GetMealRowAsync(Client client, int id)
    {
        var response = await client.From<MealDb>().Where(x => x.Id == id).Get(CancellationToken);
        return response.Models.SingleOrDefault();
    }
}
