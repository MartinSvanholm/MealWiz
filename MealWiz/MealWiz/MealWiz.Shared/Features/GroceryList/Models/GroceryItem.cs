using MealWiz.Shared.Features.Ingredients.Models;

namespace MealWiz.Shared.Features.GroceryList.Models;

public class GroceryItem
{
    public GroceryItem()
    {
        Name = string.Empty;
        Amount = string.Empty;
        IsManual = true;
    }

    public GroceryItem(GroceryItemDb groceryItemDb)
    {
        Id = groceryItemDb.Id;
        Name = groceryItemDb.Name;
        Amount = groceryItemDb.Amount ?? string.Empty;
        IsPicked = groceryItemDb.IsPicked;
        IsManual = groceryItemDb.IsManual;
        CreatedBy = groceryItemDb.CreatedBy;
        CreatedAt = groceryItemDb.CreatedAt;
        UpdatedAt = groceryItemDb.UpdatedAt;
    }

    public GroceryItem(Ingredient ingredient)
    {
        Name = ingredient.Name;
        Amount = ingredient.Amount;
        IsPicked = false;
        IsManual = false;
        CreatedBy = ingredient.CreatedBy;
        CreatedAt = DateTime.UtcNow;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string Amount { get; set; }
    public bool IsPicked { get; set; }
    /// <summary>True for items typed by hand and items from before source links; false for items added from the meal plan.</summary>
    public bool IsManual { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>A copy to edit in the drawer, so the listed item changes only on Save (MEA-76).</summary>
    public GroceryItem Clone() => (GroceryItem)MemberwiseClone();

    public GroceryItemDb MapToDb()
    {
        return new ()
        {
            Id = Id,
            Name = Name,
            Amount = Amount,
            IsPicked = IsPicked,
            IsManual = IsManual,
            CreatedBy = CreatedBy,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt
        };
    }
}