using System.Globalization;
using MealWiz.Shared.Features.Ingredients.Models;
using MealWiz.Shared.Features.MealPlans.Models;

namespace MealWiz.Shared.Features.Meals.Models;

public class Meal
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Recipe { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<Ingredient> Ingredients { get; set; }
    public MealPlan? MealPlan { get; set; }
    public DateTime? MealDate { get; set; }
    public MealType Type { get; set; } = MealType.Regular;

    public bool IsLeftover => Type == MealType.Leftover;

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
        Type = MealTypeExtensions.FromDbValue(mealDb.MealType);

        Ingredients = mealDb.Ingredients != null ? mealDb.Ingredients.Select(ingredientDb => new Ingredient(ingredientDb)).ToList() : [];
    }

    public Meal(MealDb mealDb, MealPlan mealPlan, string mealDate)
    {
        Id = mealDb.Id;
        Name = mealDb.Name;
        Recipe = mealDb.Recipe;
        CreatedBy = mealDb.CreatedBy;
        CreatedAt = mealDb.CreatedAt;
        UpdatedAt = mealDb.UpdatedAt;
        MealPlan = mealPlan;
        MealDate = DateTime.Parse(mealDate, CultureInfo.InvariantCulture);
        Type = MealTypeExtensions.FromDbValue(mealDb.MealType);

        Ingredients = mealDb.Ingredients != null ? mealDb.Ingredients.Select(ingredientDb => new Ingredient(ingredientDb)).ToList() : [];
    }

    public IReadOnlyList<string> RecipeSteps =>
        Recipe.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

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
            MealType = Type.ToDbValue(),
            Ingredients = Ingredients.Select(ingredient => ingredient.MapToIngredientDb()).ToList()
        };
    }
}