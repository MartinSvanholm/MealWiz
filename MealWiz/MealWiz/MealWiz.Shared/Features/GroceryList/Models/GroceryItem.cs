using MealWiz.Shared.Features.Ingredients.Models;

namespace MealWiz.Shared.Features.GroceryList.Models;

public class GroceryItem
{
    public GroceryItem()
    {
        Name = string.Empty;
        Amount = string.Empty;
    }

    public GroceryItem(GroceryItemDb groceryItemDb)
    {
        Id = groceryItemDb.Id;
        Name = groceryItemDb.Name;
        Amount = groceryItemDb.Amount ?? string.Empty;
        IsPicked = groceryItemDb.IsPicked;
        CreatedBy = groceryItemDb.CreatedBy;
        CreatedAt = groceryItemDb.CreatedAt;
        UpdatedAt = groceryItemDb.UpdatedAt;
    }

    public GroceryItem(Ingredient ingredient)
    {
        Name = ingredient.Name;
        Amount = ingredient.Amount;
        IsPicked = false;
        CreatedBy = ingredient.CreatedBy;
        CreatedAt = DateTime.Now;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string Amount { get; set; }
    public bool IsPicked { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public GroceryItemDb MapToDb()
    {
        return new ()
        {
            Id = Id,
            Name = Name,
            Amount = Amount,
            IsPicked = IsPicked,
            CreatedBy = CreatedBy,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}