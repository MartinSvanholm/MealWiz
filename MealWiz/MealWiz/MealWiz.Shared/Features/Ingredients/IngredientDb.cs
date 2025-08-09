using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.Ingredients;

[Table("ingredients")]
public class IngredientDb : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("amount")]
    public string Amount { get; set; }

    [Column("show_on_grocery_list")]
    public bool ShowOnGroceryList { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Column("fk_meal")]
    public int MealId { get; set; }
}