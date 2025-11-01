namespace MealWiz.Shared.Features.GroceryList.Models;

public class GroceryItem
{
    public GroceryItem()
    {
        Name = string.Empty;
        Amount = string.Empty;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string Amount { get; set; }
    public bool IsPicked { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}