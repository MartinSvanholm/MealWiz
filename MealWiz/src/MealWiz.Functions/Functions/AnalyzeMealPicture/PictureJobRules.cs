using Microsoft.Extensions.Configuration;

namespace MealWiz.Functions.Functions.AnalyzeMealPicture;

/// <summary>The pure rules of a picture job, apart from the database and Storage.</summary>
public static class PictureJobRules
{
    public const int DefaultDailyLimit = 20;
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(1);

    /// <summary><c>MealPictureImport:DailyLimit</c>: 20 by default; 0 turns analysis off; invalid or negative means 20.</summary>
    public static int DailyLimit(IConfiguration configuration) =>
        int.TryParse(configuration["MealPictureImport:DailyLimit"], out var limit) && limit >= 0 ? limit : DefaultDailyLimit;

    /// <summary>The row was created in the last five minutes (and at most a minute ahead of this clock).</summary>
    public static bool IsFresh(DateTime createdAtUtc, DateTime nowUtc)
    {
        var age = nowUtc - createdAtUtc;
        return age <= MaxAge && age >= -MaxClockSkew;
    }

    /// <summary>
    /// One to three paths, each under <c>{ownerId}/</c> (lowercase), with no empty, <c>.</c> or <c>..</c> segment and no
    /// backslash. The job-id folder isn't checked: a retry reuses the first job's pictures.
    /// </summary>
    public static bool AreValidPaths(Guid ownerId, IReadOnlyList<string>? paths) =>
        paths is { Count: >= 1 and <= Shared.Models.MealImportPictures.MaxPictures }
        && paths.All(path => IsValidPath(ownerId, path));

    private static bool IsValidPath(Guid ownerId, string? path)
    {
        if (string.IsNullOrEmpty(path) || path.Contains('\\') || path != path.ToLowerInvariant()) return false;
        var segments = path.Split('/');
        return segments.Length >= 2
            && segments[0] == ownerId.ToString("D")
            && segments.All(s => s.Length > 0 && s != "." && s != "..");
    }
}
