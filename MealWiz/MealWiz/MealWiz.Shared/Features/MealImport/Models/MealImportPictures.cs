namespace MealWiz.Shared.Features.MealImport.Models;

/// <summary>
/// The picture import's storage rules. Independently duplicated in the Function project
/// (<c>MealWiz/src/MealWiz.Functions/Shared/Models/MealImportPictures.cs</c>); keep the values in sync.
/// </summary>
public static class MealImportPictures
{
    /// <summary>The private Storage bucket (created by the migration); a user reaches only their own folder.</summary>
    public const string BucketId = "meal-import-pictures";

    public const int MaxPictures = 3;

    public const int MaxHintLength = 200;

    /// <summary>The object path of a job's picture: <c>{userId}/{jobId}/{index}.jpg</c>, lowercase.</summary>
    public static string PathFor(Guid userId, Guid jobId, int index) => $"{userId:D}/{jobId:D}/{index}.jpg";
}
