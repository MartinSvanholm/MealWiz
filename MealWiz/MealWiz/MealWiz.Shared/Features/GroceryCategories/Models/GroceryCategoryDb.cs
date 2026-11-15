using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.GroceryCategories.Models;

[Table("grocery_categories")]
public class GroceryCategoryDb : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("default_key")]
    public string? DefaultKey { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("emoji")]
    public string? Emoji { get; set; }

    [Column("absorbed_keys")]
    public List<string> AbsorbedKeys { get; set; } = [];

    [Column("position")]
    public int Position { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
