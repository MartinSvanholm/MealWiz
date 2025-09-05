namespace MealWiz.Shared.Features.Ingredients.Models;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Amount { get; set; }
    public bool ShowOnGroceryList { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int MealId { get; set; }

    public Ingredient()
    {
    }

    public Ingredient(IngredientDb ingredientDb)
    {
        Id = ingredientDb.Id;
        Name = ingredientDb.Name;
        Amount = ingredientDb.Amount;
        ShowOnGroceryList = ingredientDb.ShowOnGroceryList;
        CreatedBy = ingredientDb.CreatedBy;
        CreatedAt = ingredientDb.CreatedAt;
        UpdatedAt = ingredientDb.UpdatedAt;
        MealId = ingredientDb.MealId;
    }

    public IngredientDb MapToIngredientDb()
    {
        return new IngredientDb
        {
            Id = Id,
            Name = Name,
            Amount = Amount,
            ShowOnGroceryList = ShowOnGroceryList,
            CreatedBy = CreatedBy,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            MealId = MealId
        };
    }
}
