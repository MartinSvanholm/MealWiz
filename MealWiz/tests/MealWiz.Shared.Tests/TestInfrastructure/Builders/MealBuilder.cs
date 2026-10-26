using MealWiz.Shared.Features.Meals.Models;
using Supabase;

namespace MealWiz.Shared.Tests.TestInfrastructure.Builders;

public sealed class MealBuilder
{
    private string _name = $"Test meal {ClientExtensions.UniqueSuffix()}";
    private string _recipe = "Cook it.";
    private MealType _type = MealType.Regular;
    private readonly List<IngredientBuilder> _ingredients = [];

    public MealBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public MealBuilder WithRecipe(string recipe)
    {
        _recipe = recipe;
        return this;
    }

    public MealBuilder OfType(MealType type)
    {
        _type = type;
        return this;
    }

    public MealBuilder WithIngredient(Action<IngredientBuilder>? configure = null)
    {
        var ingredient = new IngredientBuilder();
        configure?.Invoke(ingredient);
        _ingredients.Add(ingredient);
        return this;
    }

    /// <summary>An unsaved meal (id 0), e.g. for <c>SaveMeal.Command</c>.</summary>
    public Meal Build() => new()
    {
        Name = _name,
        Recipe = _recipe,
        Type = _type,
        Ingredients = _ingredients.ConvertAll(ingredient => ingredient.Build())
    };

    /// <summary>Inserts the meal and then its ingredients as the client's signed-in user.</summary>
    public async Task<Meal> InsertAsync(Client client)
    {
        var mealDb = new Meal { Name = _name, Recipe = _recipe, Type = _type }.MapToMealDb();
        mealDb.CreatedBy = client.CurrentUserId();
        mealDb.CreatedAt = DateTime.UtcNow;

        var response = await client.From<MealDb>().Insert(mealDb);
        var meal = new Meal(response.Model ?? throw new InvalidOperationException("Inserting a meal returned no row."));

        foreach (var ingredient in _ingredients)
        {
            meal.Ingredients.Add(await ingredient.ForMeal(meal.Id).InsertAsync(client));
        }

        return meal;
    }
}
