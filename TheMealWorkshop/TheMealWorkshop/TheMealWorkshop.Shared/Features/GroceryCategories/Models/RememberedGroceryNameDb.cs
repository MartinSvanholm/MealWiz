using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheMealWorkshop.Shared.Features.GroceryCategories.Models;

[Table("grocery_remembered_names")]
public class RememberedGroceryNameDb : BaseModel
{
    [PrimaryKey("id")]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("fk_category")]
    public int CategoryId { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// A <c>grocery_remembered_names</c> row to upsert. It has no <c>id</c>: an upsert always sends the primary key, and
/// <see cref="RememberedGroceryNameDb"/>'s would send 0 (see <see cref="NewGroceryCategoryDb"/>).
/// </summary>
[Table("grocery_remembered_names")]
public class NewRememberedGroceryNameDb : BaseModel
{
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Column("fk_category")]
    public int CategoryId { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }
}
