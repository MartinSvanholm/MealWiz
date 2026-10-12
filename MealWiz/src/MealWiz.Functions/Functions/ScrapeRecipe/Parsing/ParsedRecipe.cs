namespace MealWiz.Functions.Functions.ScrapeRecipe.Parsing;

public class ParsedRecipe
{
    public required string Name { get; init; }
    public string? Recipe { get; init; }
    public List<ParsedIngredient> Ingredients { get; init; } = [];
}

public class ParsedIngredient
{
    public required string Name { get; init; }
}
