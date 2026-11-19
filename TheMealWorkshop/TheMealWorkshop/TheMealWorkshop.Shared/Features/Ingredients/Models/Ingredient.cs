using TheMealWorkshop.Shared.Amounts;

namespace TheMealWorkshop.Shared.Features.Ingredients.Models;

public class Ingredient
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Amount { get; set; }
    public bool ShowOnGroceryList { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
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

    /// <summary>A copy to edit in the drawer, so the listed ingredient changes only on Save (MEA-77).</summary>
    public Ingredient Clone() => (Ingredient)MemberwiseClone();

    /// <summary>Takes over the fields the ingredient drawer edits.</summary>
    public void CopyEditableFieldsFrom(Ingredient other)
    {
        Name = other.Name;
        Amount = other.Amount;
        ShowOnGroceryList = other.ShowOnGroceryList;
    }

    public IngredientDb MapToIngredientDb()
    {
        var amount = ParsedAmount.Parse(Amount);
        return new IngredientDb
        {
            Id = Id,
            Name = Name,
            Amount = Amount,
            AmountQuantity = amount.Quantity,
            AmountUnit = amount.UnitCode,
            ShowOnGroceryList = ShowOnGroceryList,
            CreatedBy = CreatedBy,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            MealId = MealId
        };
    }
}
