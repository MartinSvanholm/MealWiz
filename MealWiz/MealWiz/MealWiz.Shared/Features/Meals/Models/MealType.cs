namespace MealWiz.Shared.Features.Meals.Models;

public enum MealType
{
    Regular = 0,
    Leftover = 1,
}

public static class MealTypeExtensions
{
    public const string RegularValue = "regular";
    public const string LeftoverValue = "leftover";

    public static string ToDbValue(this MealType type) => type switch
    {
        MealType.Leftover => LeftoverValue,
        _ => RegularValue,
    };

    public static MealType FromDbValue(string? value) => value switch
    {
        LeftoverValue => MealType.Leftover,
        _ => MealType.Regular,
    };
}
