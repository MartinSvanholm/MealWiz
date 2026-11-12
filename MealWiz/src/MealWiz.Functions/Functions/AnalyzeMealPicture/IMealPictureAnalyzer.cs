using FluentResults;
using MealWiz.Functions.Shared.Models;

namespace MealWiz.Functions.Functions.AnalyzeMealPicture;

/// <summary>One to three JPEG pictures of one meal, in the order the user added them.</summary>
public record MealPictureAnalysisRequest(IReadOnlyList<byte[]> Pictures, string Language, string? Hint);

/// <summary>
/// The AI seam: reads a written recipe or guesses a dish from the pictures. A failure carries a
/// <see cref="PictureImportError"/> (NoMealFound, PictureUnreadable, AiUnavailable or TimedOut). Tests use a fake.
/// </summary>
public interface IMealPictureAnalyzer
{
    Task<Result<MealImportPayload>> AnalyzeAsync(MealPictureAnalysisRequest request, CancellationToken cancellationToken);
}

/// <summary>An error that knows the <c>error_code</c> the job row gets.</summary>
public class PictureImportError(RecipeImportErrorCode code, string message) : Error(message)
{
    public RecipeImportErrorCode Code { get; } = code;
}
