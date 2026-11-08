using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.MealPlans.Models;

/// <summary>A slim read of <c>meal_plans-meals</c>: which meal is planned on which date, without the meal itself.</summary>
[Table("meal_plans-meals")]
public class PlannedMealDateDb : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("meal_date")]
    public string MealDate { get; set; } = string.Empty;

    [Column("fk_meal")]
    public int MealId { get; set; }
}
