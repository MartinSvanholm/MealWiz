using FluentResults;
using MealWiz.Functions.Shared.Models;

namespace MealWiz.Functions.Functions.AnalyzeMealPicture;

public static class MealPictureMapper
{
    /// <summary>
    /// The reading as a payload: <c>no_meal</c> or a blank name is NoMealFound; otherwise values are trimmed,
    /// ingredients without a name dropped, line endings normalised, and IsGuessed is true for a dish.
    /// </summary>
    public static Result<MealImportPayload> Map(MealPictureReading reading)
    {
        var name = (reading.Name ?? string.Empty).Trim();
        if (reading.Kind == MealPictureKind.NoMeal || name.Length == 0)
            return Result.Fail<MealImportPayload>(new PictureImportError(
                RecipeImportErrorCode.NoMealFound, "The pictures show no meal or recipe."));

        return Result.Ok(new MealImportPayload
        {
            Name = name,
            Recipe = NormalizeLines(reading.Recipe),
            IsGuessed = reading.Kind == MealPictureKind.Dish,
            Ingredients = (reading.Ingredients ?? [])
                .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                .Select(i => new MealImportIngredientPayload
                {
                    Name = i.Name.Trim(),
                    Amount = (i.Amount ?? string.Empty).Trim()
                })
                .ToList()
        });
    }

    private static string NormalizeLines(string? text) =>
        string.Join('\n', (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n')
            .Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
}
