using MealWiz.Shared.Features.Ingredients.Models;
using Supabase;

namespace MealWiz.Shared.Tests.TestInfrastructure.Builders;

public sealed class IngredientBuilder
{
    private string _name = $"Test ingredient {ClientExtensions.UniqueSuffix()}";
    private string _amount = "1";
    private bool _showOnGroceryList = true;
    private int _mealId;

    public IngredientBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public IngredientBuilder WithAmount(string amount)
    {
        _amount = amount;
        return this;
    }

    public IngredientBuilder ShowOnGroceryList(bool show = true)
    {
        _showOnGroceryList = show;
        return this;
    }

    public IngredientBuilder ForMeal(int mealId)
    {
        _mealId = mealId;
        return this;
    }

    public Ingredient Build() => new()
    {
        Name = _name,
        Amount = _amount,
        ShowOnGroceryList = _showOnGroceryList,
        MealId = _mealId
    };

    /// <summary>Inserts the ingredient as the client's signed-in user. Needs <see cref="ForMeal"/> first.</summary>
    public async Task<Ingredient> InsertAsync(Client client)
    {
        if (_mealId == 0) throw new InvalidOperationException("Call ForMeal before inserting an ingredient.");

        var ingredientDb = Build().MapToIngredientDb();
        ingredientDb.CreatedBy = client.CurrentUserId();
        ingredientDb.CreatedAt = DateTime.UtcNow;

        var response = await client.From<IngredientDb>().Insert(ingredientDb);
        return new Ingredient(response.Model ?? throw new InvalidOperationException("Inserting an ingredient returned no row."));
    }
}
