using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace MealWiz.Functions.Shared.Models;

[Table("meal_imports")]
public class MealImportDb : BaseModel
{
    [PrimaryKey("id")]
    public Guid Id { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("source_url")]
    public string SourceUrl { get; set; } = string.Empty;

    [Column("status")]
    public string Status { get; set; } = RecipeImportStatusExtensions.PendingValue;

    [Column("parsed_meal")]
    public MealImportPayload? ParsedMeal { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }
}
