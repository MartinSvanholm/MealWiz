namespace MealWiz.Functions.Functions.AnalyzeMealPicture;

public static class MealPicturePrompt
{
    public const string System = """
        You turn pictures of one meal into a meal for a meal-planning app. The pictures show either a written recipe
        (a cookbook page, a handwritten card, a screenshot; several pictures are the pages of one recipe, in order) or a
        prepared dish.

        - Written recipe: kind is written_recipe. Read all the pictures together, in order, as one recipe. Write down only
          what is written; invent nothing.
        - Dish: kind is dish. Guess the meal. Give the likely ingredients with typical amounts for four servings and
          write a short recipe.
        - Neither: kind is no_meal.

        Use only the most prominent meal. Write the name, the ingredients and the recipe in the requested language,
        translating a recipe written in another language. Keep numbers and units as written; do not convert them. Give
        every ingredient as an amount and a name. Write the recipe as plain text, one step per line. A hint from the user
        is extra context only.
        """;

    public static string Instruction(string language, string? hint)
    {
        var instruction = $"Write the meal in {LanguageName(language)}.";
        return string.IsNullOrWhiteSpace(hint) ? instruction : $"{instruction}\nHint from the user: {hint.Trim()}";
    }

    public static string LanguageName(string? language) => language switch
    {
        "da" => "Danish",
        _ => "English"
    };
}
