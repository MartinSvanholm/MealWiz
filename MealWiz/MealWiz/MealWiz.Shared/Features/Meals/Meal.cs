using MealWiz.Shared.Features.Ingredients;

namespace MealWiz.Shared.Features.Meals;

public class Meal
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<Ingredient> Ingredients { get; set; }

    public Meal()
    {
        Id = 0;
        Name = string.Empty;
        Description = string.Empty;
        Ingredients = [];
    }

    public Meal(MealDb mealDb)
    {
        Id = mealDb.Id;
        Name = mealDb.Name;
        Description = mealDb.Description;
        CreatedBy = mealDb.CreatedBy;
        CreatedAt = mealDb.CreatedAt;
        UpdatedAt = mealDb.UpdatedAt;

        if (Ingredients != null)
        {
            Ingredients = mealDb.Ingredients.Select(ingredientDb => new Ingredient(ingredientDb)).ToList();
        }
        else
        {
            Ingredients = [];
        }

    }
}