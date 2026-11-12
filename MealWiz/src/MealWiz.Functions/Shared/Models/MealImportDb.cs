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

    // Null for a picture job.
    [Column("source_url")]
    public string? SourceUrl { get; set; }

    // A picture job: storage paths in the order the user added them, the app language and an optional hint.
    // All three are null for a link job.
    [Column("picture_paths")]
    public List<string>? PicturePaths { get; set; }

    [Column("language")]
    public string? Language { get; set; }

    [Column("hint")]
    public string? Hint { get; set; }

    [Column("status")]
    public string Status { get; set; } = RecipeImportStatusExtensions.PendingValue;

    [Column("parsed_meal")]
    public MealImportPayload? ParsedMeal { get; set; }

    [Column("error_code")]
    public string? ErrorCode { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }
}
