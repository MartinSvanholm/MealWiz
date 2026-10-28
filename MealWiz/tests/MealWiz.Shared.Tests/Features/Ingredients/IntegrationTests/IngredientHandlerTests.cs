using MealWiz.Shared.Features.Ingredients.DeleteIngredient;
using MealWiz.Shared.Features.Ingredients.GetIngredientsByMealId;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.Ingredients.SaveIngredient;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using MealWiz.Shared.Tests.TestInfrastructure.Builders;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Ingredients.IntegrationTests;

/// <summary>
/// Runs the Ingredients handlers as a freshly signed-in user. The tests share the class's user, so each one only
/// looks at the meals and ingredients it created.
/// </summary>
[Trait("Category", "Integration")]
public class IngredientHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SaveIngredient_NewIngredient_AttachesItToTheMeal()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);
        var before = DateTime.UtcNow;

        // What the Create Ingredient card sends: an unsaved ingredient (id 0) plus the meal being edited.
        var ingredient = new IngredientBuilder().WithName("Basil").WithAmount("1 bunch").ShowOnGroceryList(false).Build();
        var result = await SaveAsync(ingredient, meal.Id);

        ResultAssert.Ok(result);
        var row = Assert.Single(await GetRowsAsync(meal.Id));
        Assert.Equal("Basil", row.Name);
        Assert.Equal("1 bunch", row.Amount);
        Assert.False(row.ShowOnGroceryList);
        Assert.Equal(supabase.UserId, row.CreatedBy);
        TimeAssert.CloseTo(before, row.CreatedAt);
    }

    [Fact]
    public async Task SaveIngredient_ExistingIngredient_UpdatesItAndIgnoresTheMealId()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().WithIngredient(i => i.WithName("Salt").WithAmount("1 tsp")).InsertAsync(supabase.Client);
        var otherMeal = await new MealBuilder().InsertAsync(supabase.Client);
        var ingredient = meal.Ingredients.Single();
        var before = DateTime.UtcNow;

        ingredient.Name = "Sea salt";
        ingredient.Amount = "2 tsp";
        var result = await SaveAsync(ingredient, otherMeal.Id);

        ResultAssert.Ok(result);
        var row = Assert.Single(await GetRowsAsync(meal.Id));
        Assert.Equal(ingredient.Id, row.Id);
        Assert.Equal("Sea salt", row.Name);
        Assert.Equal("2 tsp", row.Amount);
        TimeAssert.CloseTo(before, row.UpdatedAt);
        Assert.Empty(await GetRowsAsync(otherMeal.Id));
    }

    [Fact]
    public async Task SaveIngredient_MealThatDoesNotExist_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);

        var result = await SaveAsync(new IngredientBuilder().Build(), int.MaxValue);

        // The fk_meal foreign key rejects it, and the error comes back as a failed result.
        ResultAssert.Failed(result);
    }

    [Fact]
    public async Task SaveIngredient_NewIngredientSignedOut_FailsWithNotSignedIn()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await new SaveIngredient.Handler(signedOut)
            .Handle(new SaveIngredient.Command(new IngredientBuilder().Build(), meal.Id), CancellationToken);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }

    [Fact]
    public async Task GetIngredientsByMealId_ReturnsOnlyThatMealsIngredients()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder()
            .WithIngredient(i => i.WithName("Rice"))
            .WithIngredient(i => i.WithName("Beans"))
            .InsertAsync(supabase.Client);
        await new MealBuilder().WithIngredient(i => i.WithName("Not this one")).InsertAsync(supabase.Client);

        var ingredients = ResultAssert.Ok(await GetByMealIdAsync(meal.Id));

        Assert.Equal(["Beans", "Rice"], ingredients.Select(i => i.Name).Order());
        Assert.All(ingredients, i => Assert.Equal(meal.Id, i.MealId));
    }

    [Fact]
    public async Task GetIngredientsByMealId_MealWithoutIngredients_ReturnsAnEmptyList()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder().InsertAsync(supabase.Client);

        var ingredients = ResultAssert.Ok(await GetByMealIdAsync(meal.Id));

        Assert.Empty(ingredients);
    }

    [Fact]
    public async Task DeleteIngredient_RemovesOnlyThatIngredient()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var meal = await new MealBuilder()
            .WithIngredient(i => i.WithName("Keep"))
            .WithIngredient(i => i.WithName("Remove"))
            .InsertAsync(supabase.Client);
        var toDelete = meal.Ingredients.Single(i => i.Name == "Remove");

        var result = await new DeleteIngredient.Handler(supabase.Client).Handle(new DeleteIngredient.Command(toDelete), CancellationToken);

        ResultAssert.Ok(result);
        var remaining = Assert.Single(await GetRowsAsync(meal.Id));
        Assert.Equal("Keep", remaining.Name);
    }

    private Task<FluentResults.Result> SaveAsync(Ingredient ingredient, int mealId) =>
        new SaveIngredient.Handler(supabase.Client).Handle(new SaveIngredient.Command(ingredient, mealId), CancellationToken);

    private Task<FluentResults.Result<List<Ingredient>>> GetByMealIdAsync(int mealId) =>
        new GetIngredientsByMealId.Handler(supabase.Client).Handle(new GetIngredientsByMealId.Query(mealId), CancellationToken);

    private async Task<List<IngredientDb>> GetRowsAsync(int mealId)
    {
        var response = await supabase.Client.From<IngredientDb>().Where(x => x.MealId == mealId).Get(CancellationToken);
        return response.Models;
    }
}
