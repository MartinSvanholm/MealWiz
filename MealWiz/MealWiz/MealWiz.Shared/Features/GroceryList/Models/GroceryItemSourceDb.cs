using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>The write model of grocery_item_sources: insert, update and delete through this one (it has no references).</summary>
[Table("grocery_item_sources")]
public class GroceryItemSourceDb : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("fk_grocery_item")]
    public int GroceryItemId { get; set; }

    [Column("fk_planned_meal")]
    public int? PlannedMealId { get; set; }

    [Column("fk_ingredient")]
    public int? IngredientId { get; set; }

    [Column("amount")]
    public string Amount { get; set; } = string.Empty;

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
