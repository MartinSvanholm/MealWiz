namespace TheMealWorkshop.Shared.Features.GroceryCategories.Models;

/// <summary>A grocery name the user put in a category; it beats the dictionary. <see cref="Name"/> is normalised.</summary>
public record RememberedGroceryName(string Name, int CategoryId)
{
    public RememberedGroceryName(RememberedGroceryNameDb db) : this(db.Name, db.CategoryId)
    {
    }
}
