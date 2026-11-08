using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.GroceryList.Models;

/// <summary>
/// The read model of grocery_item_sources: the row plus the planned day and ingredient it points at, each with its
/// meal. GET only; references can't be inserted or deleted through it.
/// </summary>
[Table("grocery_item_sources")]
public class GroceryItemSourceDetailsDb : BaseModel
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

    [Reference(typeof(PlannedDayDb), ReferenceAttribute.JoinType.Left)]
    public PlannedDayDb? PlannedDay { get; set; }

    [Reference(typeof(SourceIngredientDb), ReferenceAttribute.JoinType.Left)]
    public SourceIngredientDb? Ingredient { get; set; }

    [Table("meal_plans-meals")]
    public class PlannedDayDb : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("meal_date")]
        public string MealDate { get; set; } = string.Empty;

        [Column("fk_meal")]
        public int MealId { get; set; }

        [Reference(typeof(SourceMealDb), ReferenceAttribute.JoinType.Left)]
        public SourceMealDb? Meal { get; set; }
    }

    [Table("ingredients")]
    public class SourceIngredientDb : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("amount")]
        public string? Amount { get; set; }

        [Column("fk_meal")]
        public int MealId { get; set; }

        [Reference(typeof(SourceMealDb), ReferenceAttribute.JoinType.Left)]
        public SourceMealDb? Meal { get; set; }
    }

    [Table("meals")]
    public class SourceMealDb : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;
    }
}
