using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.GroceryCategories.Models;

[Table("grocery_list_settings")]
public class GroceryListSettingsDb : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("order_mode")]
    public string OrderMode { get; set; } = SectionOrderModeExtensions.LearnedValue;

    [Column("learned_until")]
    public DateTime? LearnedUntil { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// The order mode to upsert on (<c>created_by</c>). No <c>id</c> (an upsert always sends the primary key) and no
/// <c>learned_until</c>, so changing the mode keeps what was learned.
/// </summary>
[Table("grocery_list_settings")]
public class GroceryListOrderModeDb : BaseModel
{
    [Column("order_mode")]
    public string OrderMode { get; set; } = SectionOrderModeExtensions.LearnedValue;

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
