namespace MealWiz.Shared.Features.MealImport.Models;

public class MealImport
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public string SourceUrl { get; set; }
    public MealImportStatus Status { get; set; } = MealImportStatus.Pending;
    public ParsedMeal? ParsedMeal { get; set; }
    public MealImportErrorCode? ErrorCode { get; set; }

    /// <summary>The English detail, for diagnosis only. Show <see cref="MealImportErrorMessages.ToMessage"/> instead.</summary>
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool IsResolved => Status != MealImportStatus.Pending;

    public MealImport()
    {
        SourceUrl = string.Empty;
    }

    public MealImport(MealImportDb mealImportDb)
    {
        Id = mealImportDb.Id;
        CreatedBy = mealImportDb.CreatedBy;
        SourceUrl = mealImportDb.SourceUrl;
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
