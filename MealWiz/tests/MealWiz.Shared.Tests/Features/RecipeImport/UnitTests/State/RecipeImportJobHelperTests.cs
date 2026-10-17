using MealWiz.Shared.Features.RecipeImport.Models;
using MealWiz.Shared.Features.RecipeImport.State;
using MealWiz.Shared.Helpers;
using Xunit;
using RecipeImportModel = MealWiz.Shared.Features.RecipeImport.Models.RecipeImport;

namespace MealWiz.Shared.Tests.Features.RecipeImport.UnitTests.State;

public class RecipeImportJobHelperTests
{
    [Theory]
    [InlineData("https://example.com/recipe", "https://example.com/recipe")]
    [InlineData("  http://example.com/a b  ", "http://example.com/a%20b")]
    [InlineData("https://example.com/a%20b", "https://example.com/a%20b")]
    public void TryNormalizeRecipeUrl_AcceptsHttpUrlsAndKeepsThemEscaped(string url, string expected)
    {
        Assert.True(RecipeImportJobHelper.TryNormalizeRecipeUrl(url, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(false, false, RecipeImportEntryState.Default)]
    [InlineData(false, true, RecipeImportEntryState.Failed)]
    [InlineData(true, false, RecipeImportEntryState.InProgress)]
    [InlineData(true, true, RecipeImportEntryState.InProgress)]
    public void GetEntryState_InProgressWinsOverAnEarlierFailure(bool inProgress, bool latestFailed, RecipeImportEntryState expected)
    {
        Assert.Equal(expected, RecipeImportJobHelper.GetEntryState(inProgress, latestFailed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/recipe")]
    [InlineData("/relative/path")]
    public void TryNormalizeRecipeUrl_RejectsAnythingElse(string? url)
    {
        Assert.False(RecipeImportJobHelper.TryNormalizeRecipeUrl(url, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void IsStalePendingCopy_OnlyForAPendingCopyOfTheSameResolvedImport()
    {
        var id = Guid.NewGuid();
        var resolved = new RecipeImportModel { Id = id, Status = RecipeImportStatus.Failed };

        Assert.True(RecipeImportJobHelper.IsStalePendingCopy(new RecipeImportModel { Id = id, Status = RecipeImportStatus.Pending }, resolved));
        Assert.False(RecipeImportJobHelper.IsStalePendingCopy(new RecipeImportModel { Id = id, Status = RecipeImportStatus.Failed }, resolved));
        Assert.False(RecipeImportJobHelper.IsStalePendingCopy(new RecipeImportModel { Id = Guid.NewGuid(), Status = RecipeImportStatus.Pending }, resolved));
        Assert.False(RecipeImportJobHelper.IsStalePendingCopy(new RecipeImportModel { Id = id, Status = RecipeImportStatus.Pending }, new RecipeImportModel { Id = id }));
        Assert.False(RecipeImportJobHelper.IsStalePendingCopy(null, resolved));
        Assert.False(RecipeImportJobHelper.IsStalePendingCopy(new RecipeImportModel { Id = id }, null));
    }

    [Fact]
    public void EnsureUsableResult_SucceededWithoutPayload_BecomesFailed()
    {
        var row = RecipeImportJobHelper.EnsureUsableResult(new RecipeImportModel { Status = RecipeImportStatus.Succeeded });

        Assert.Equal(RecipeImportStatus.Failed, row.Status);
        Assert.Equal(ErrorMessages.GenericError, row.ErrorMessage);
    }

    [Fact]
    public void EnsureUsableResult_LeavesUsableRowsAlone()
    {
        var succeeded = new RecipeImportModel { Status = RecipeImportStatus.Succeeded, ParsedMeal = new ParsedRecipeMeal() };
        var failed = new RecipeImportModel { Status = RecipeImportStatus.Failed, ErrorMessage = "Nope" };

        Assert.Equal(RecipeImportStatus.Succeeded, RecipeImportJobHelper.EnsureUsableResult(succeeded).Status);
        Assert.Equal("Nope", RecipeImportJobHelper.EnsureUsableResult(failed).ErrorMessage);
    }

    [Fact]
    public void CreateLocallyFailedImport_KeepsWhatIsKnownAboutTheRow()
    {
        var jobId = Guid.NewGuid();
        var current = new RecipeImportModel
        {
            Id = jobId,
            CreatedBy = Guid.NewGuid(),
            SourceUrl = "https://example.com/recipe",
            CreatedAt = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc)
        };

        var failed = RecipeImportJobHelper.CreateLocallyFailedImport(jobId, current, "Offline", DateTime.UtcNow);

        Assert.Equal(jobId, failed.Id);
        Assert.Equal(current.CreatedBy, failed.CreatedBy);
        Assert.Equal(current.SourceUrl, failed.SourceUrl);
        Assert.Equal(current.CreatedAt, failed.CreatedAt);
        Assert.Equal(RecipeImportStatus.Failed, failed.Status);
        Assert.Equal("Offline", failed.ErrorMessage);
    }

    [Fact]
    public void CreateLocallyFailedImport_WithoutACurrentRow_UsesNow()
    {
        var now = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);

        var failed = RecipeImportJobHelper.CreateLocallyFailedImport(Guid.NewGuid(), null, "Offline", now);

        Assert.Equal(now, failed.CreatedAt);
        Assert.Equal(string.Empty, failed.SourceUrl);
    }
}
