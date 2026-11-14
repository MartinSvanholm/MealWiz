namespace MealWiz.Shared.Features.MealImport.Models;

public class MealImport
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }

    /// <summary>The recipe link of a link job; null for a picture job.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>A picture job's storage paths, in the order the user added them; null for a link job.</summary>
    public IReadOnlyList<string>? PicturePaths { get; set; }

    /// <summary>The app language ("en" or "da") a picture job's meal is written in.</summary>
    public string? Language { get; set; }

    /// <summary>The user's hint for a picture job's retry.</summary>
    public string? Hint { get; set; }
    public MealImportStatus Status { get; set; } = MealImportStatus.Pending;
    public ParsedMeal? ParsedMeal { get; set; }
    public MealImportErrorCode? ErrorCode { get; set; }

    /// <summary>The English detail, for diagnosis only. Show <see cref="MealImportErrorMessages.ToMessage"/> instead.</summary>
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool IsResolved => Status != MealImportStatus.Pending;

    public bool IsPictureImport => PicturePaths is { Count: > 0 };

    public MealImport()
    {
    }

    public MealImport(MealImportDb mealImportDb)
    {
        Id = mealImportDb.Id;
        CreatedBy = mealImportDb.CreatedBy;
        SourceUrl = mealImportDb.SourceUrl;
        PicturePaths = mealImportDb.PicturePaths;
        Language = mealImportDb.Language;
        Hint = mealImportDb.Hint;
        Status = MealImportStatusExtensions.FromDbValue(mealImportDb.Status);
        ParsedMeal = mealImportDb.ParsedMeal;
        ErrorCode = MealImportErrorCodeExtensions.FromDbValue(mealImportDb.ErrorCode);
        ErrorMessage = mealImportDb.ErrorMessage;
        CreatedAt = mealImportDb.CreatedAt;
        UpdatedAt = mealImportDb.UpdatedAt;
        UpdatedBy = mealImportDb.UpdatedBy;
    }

    public MealImportDb MapToMealImportDb()
    {
        return new MealImportDb
        {
            Id = Id,
            CreatedBy = CreatedBy,
            SourceUrl = SourceUrl,
            PicturePaths = PicturePaths?.ToList(),
            Language = Language,
            Hint = Hint,
            Status = Status.ToDbValue(),
            ParsedMeal = ParsedMeal,
            ErrorCode = ErrorCode?.ToDbValue(),
            ErrorMessage = ErrorMessage,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            UpdatedBy = UpdatedBy
        };
    }
}
