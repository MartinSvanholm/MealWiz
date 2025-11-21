namespace MealWiz.Shared.Features.GroceryList.Models;

public class GroceryList
{
    public GroceryList()
    {
        Items = [];
    }

    public int Id { get; set; }
    public List<GroceryItem> Items { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
    public void SortItemsByIsPicked()
    {
        Items = [.. Items.OrderBy(item => item.IsPicked)];
    }
}