using MealWiz.Shared.Features.Ingredients;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.Meals;

[Table("meals")]
public class MealDb : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; }

    [Column("description")]
    public string Description { get; set; }

    [Column("created_by")]
    public Guid CreatedByt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
    public List<IngredientDb> Ingredients { get; set; }
}