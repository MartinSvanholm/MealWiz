using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Shared.Features.MealImport.Models;

[Table("meal_imports")]
public class MealImportDb : BaseModel
{
    // Client-generated, so it has to be sent on insert (Postgrest skips primary keys by default).
    [PrimaryKey("id", true)]
    public Guid Id { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("source_url")]
    public string SourceUrl { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = MealImportStatusExtensions.PendingValue;

    // jsonb — Newtonsoft (de)serializes the object directly.
    [Column("parsed_meal")]
    public ParsedMeal? ParsedMeal { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }
}
