namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>
/// One linked ingredient whose amount changed. <paramref name="SourceAmount"/> is the ingredient's current amount (what the
/// link remembers afterwards); <paramref name="FinalAmount"/> is the grocery item's new total.
/// </summary>
public record LinkedAmountUpdate(int SourceId, int GroceryItemId, string SourceAmount, string FinalAmount);
