namespace MealWizFeatures.Features.Ingredients;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Amount { get; set; }
    public bool ShowOnGroceryList { get; set; }
    public Guid CreatedByt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Ingredient()
    {
        Id = 0;
        Name = string.Empty;
        Amount = string.Empty;
    }

    public Ingredient(IngredientDb ingredientDb)
    {
        Id = ingredientDb.Id;
        Name = ingredientDb.Name;
        Amount = ingredientDb.Amount;
        ShowOnGroceryList = ingredientDb.ShowOnGroceryList;
        CreatedByt = ingredientDb.CreatedByt;
        CreatedAt = ingredientDb.CreatedAt;
        UpdatedAt = ingredientDb.UpdatedAt;
    }
}
