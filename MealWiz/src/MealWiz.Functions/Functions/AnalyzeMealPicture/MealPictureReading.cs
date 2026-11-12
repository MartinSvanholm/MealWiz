using Anthropic.Helpers;

namespace MealWiz.Functions.Functions.AnalyzeMealPicture;

/// <summary>What the picture shows. The SDK turns the names into snake_case in the JSON schema.</summary>
public enum MealPictureKind
{
    WrittenRecipe,
    Dish,
    NoMeal
}

/// <summary>The structured answer the AI service must give; the SDK infers the JSON schema from this type.</summary>
public class MealPictureReading
{
    [SchemaProperty("written_recipe: the pictures show a written recipe. dish: they show a prepared dish. no_meal: neither.")]
    public MealPictureKind Kind { get; set; }

    [SchemaProperty("The name of the meal, in the requested language.")]
    public string Name { get; set; } = string.Empty;

    public List<MealPictureReadingIngredient> Ingredients { get; set; } = [];

    [SchemaProperty("The recipe as plain text, one step per line.")]
    public string Recipe { get; set; } = string.Empty;
}

public class MealPictureReadingIngredient
{
    [SchemaProperty("The amount with its unit as written, such as '2 dl' or '500 g'. Empty when there is none.")]
    public string Amount { get; set; } = string.Empty;

    [SchemaProperty("The ingredient name without the amount.")]
    public string Name { get; set; } = string.Empty;
}
