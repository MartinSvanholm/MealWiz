namespace MealWiz.Shared.Features.RecipeImport.Models;

public class RecipeImport
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public string SourceUrl { get; set; }
    public RecipeImportStatus Status { get; set; } = RecipeImportStatus.Pending;
    public ParsedRecipeMeal? ParsedMeal { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }

    public bool IsResolved => Status != RecipeImportStatus.Pending;

    public RecipeImport()
    {
        SourceUrl = string.Empty;
    }

    public RecipeImport(RecipeImportDb recipeImportDb)
    {
        Id = recipeImportDb.Id;
        CreatedBy = recipeImportDb.CreatedBy;
        SourceUrl = recipeImportDb.SourceUrl;
        Status = RecipeImportStatusExtensions.FromDbValue(recipeImportDb.Status);
        ParsedMeal = recipeImportDb.ParsedMeal;
        ErrorMessage = recipeImportDb.ErrorMessage;
        CreatedAt = recipeImportDb.CreatedAt;
        UpdatedAt = recipeImportDb.UpdatedAt;
        UpdatedBy = recipeImportDb.UpdatedBy;
    }

    public RecipeImportDb MapToRecipeImportDb()
    {
        return new RecipeImportDb
        {
            Id = Id,
            CreatedBy = CreatedBy,
            SourceUrl = SourceUrl,
            Status = Status.ToDbValue(),
            ParsedMeal = ParsedMeal,
            ErrorMessage = ErrorMessage,
            CreatedAt = CreatedAt,
            UpdatedAt = UpdatedAt,
            UpdatedBy = UpdatedBy
        };
    }
}
