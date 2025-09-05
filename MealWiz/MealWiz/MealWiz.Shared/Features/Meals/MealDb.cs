using MealWiz.Shared.Features.Ingredients.Models;
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

    [Column("recipe")]
    public string Recipe { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Reference(typeof(IngredientDb), ReferenceAttribute.JoinType.Left)]
    public List<IngredientDb> Ingredients { get; set; }
}