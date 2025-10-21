using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.MealPlans.Models;

namespace MealWiz.Shared.Features.Meals.Models;

public class Meal
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Recipe { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<Ingredient> Ingredients { get; set; }
    public MealPlan? MealPlan { get; set; }
    public DateTime? MealDate { get; set; }

    public Meal()
    {
        Id = 0;
        Name = string.Empty;
        Recipe = string.Empty;
        Ingredients = [];
    }

    public Meal(MealDb mealDb)
    {
        Id = mealDb.Id;
        Name = mealDb.Name;
        Recipe = mealDb.Recipe;
        CreatedBy = mealDb.CreatedBy;
        CreatedAt = mealDb.CreatedAt;
        UpdatedAt = mealDb.UpdatedAt;

        Ingredients = mealDb.Ingredients != null ? mealDb.Ingredients.ConvertAll(ingredientDb => new Ingredient(ingredientDb)) : [];
    }

    public MealDb MapToMealDb()
    {
        return new MealDb
        {
            Id = Id,
            Name = Name,
            Recipe = Recipe,
            CreatedBy = CreatedBy,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            Ingredients = Ingredients.ConvertAll(ingredient => ingredient.MapToIngredientDb())
        };
    }
}