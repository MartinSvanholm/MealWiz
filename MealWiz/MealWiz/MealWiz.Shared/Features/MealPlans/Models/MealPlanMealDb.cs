using MealWiz.Shared.Features.Meals.Models;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.MealPlans.Models;

[Table("meal_plans-meals")]
public class MealPlanMealDb : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("meal_date")]
    public DateTime MealDate { get; set; }

    [Column("fk_meal")]
    public int MealId { get; set; }

    [Column("fk_meal_plan")]
    public int MealPlanId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Reference(typeof(MealDb), ReferenceAttribute.JoinType.Left)]
    public MealDb Meal { get; set; }
}