namespace MealWiz.Functions.Shared.Models;

/// <summary>
/// The picture import's storage rules. Independently duplicated on the client
/// (<c>MealWiz/MealWiz/MealWiz.Shared/Features/MealImport/Models/MealImportPictures.cs</c>); keep the values in sync.
/// </summary>
public static class MealImportPictures
{
    public const string BucketId = "meal-import-pictures";

    public const int MaxPictures = 3;

    public const int MaxHintLength = 200;
}
