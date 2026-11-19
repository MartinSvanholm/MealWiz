using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace TheMealWorkshop.Shared.Features.GroceryCategories.Models;

/// <summary>
/// A <c>grocery_categories</c> row to insert with an upsert. It has no <c>id</c>: an upsert always sends the primary
/// key, and <see cref="GroceryCategoryDb"/>'s would send 0 for every row instead of letting the database number them.
/// </summary>
[Table("grocery_categories")]
public class NewGroceryCategoryDb : BaseModel
{
    [Column("default_key")]
    public string? DefaultKey { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("emoji")]
    public string? Emoji { get; set; }

    [Column("position")]
    public int Position { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
