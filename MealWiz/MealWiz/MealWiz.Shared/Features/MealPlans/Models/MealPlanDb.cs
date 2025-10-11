using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.MealPlans.Models;

[Table("meal_plans")]
public class MealPlanDb : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [Reference(typeof(MealPlanMealDb), ReferenceAttribute.JoinType.Left)]
    public List<MealPlanMealDb> MealPlanMeals { get; set; }
}