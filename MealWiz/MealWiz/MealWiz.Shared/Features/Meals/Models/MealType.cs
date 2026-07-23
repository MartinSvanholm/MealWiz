namespace MealWiz.Shared.Features.Meals.Models;

public enum MealType
{
    Regular = 0,
    Leftover = 1,
    EatOut = 2,
}

public static class MealTypeExtensions
{
    public const string RegularValue = "regular";
    public const string LeftoverValue = "leftover";
    public const string EatOutValue = "eat_out";

    public static string ToDbValue(this MealType type) => type switch
    {
        MealType.Leftover => LeftoverValue,
        MealType.EatOut => EatOutValue,
        _ => RegularValue,
    };

    public static MealType FromDbValue(string? value) => value switch
    {
        LeftoverValue => MealType.Leftover,
        EatOutValue => MealType.EatOut,
        _ => MealType.Regular,
    };
}
