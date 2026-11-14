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

    // Null for a picture job.
    [Column("source_url")]
    public string? SourceUrl { get; set; }

    // A picture job: the storage paths in the order the user added them, the app language and an optional hint.
    // All three are null for a link job. The client can set them on insert only.
    [Column("picture_paths")]
    public List<string>? PicturePaths { get; set; }

    [Column("language")]
    public string? Language { get; set; }

    [Column("hint")]
    public string? Hint { get; set; }

    [Column("status")]
    public string Status { get; set; } = MealImportStatusExtensions.PendingValue;

    // jsonb — Newtonsoft (de)serializes the object directly.
    [Column("parsed_meal")]
    public ParsedMeal? ParsedMeal { get; set; }

    [Column("error_code")]
    public string? ErrorCode { get; set; }

    // The English detail, for diagnosis only; the UI shows the localized message for ErrorCode.
    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    // Set by the database on insert (a trigger forces now() for a client's insert), so it is never sent.
    [Column("created_at", ignoreOnInsert: true)]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("updated_by")]
    public Guid? UpdatedBy { get; set; }
}
