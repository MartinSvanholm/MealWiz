using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Features.MealImport.State;
using MealWiz.Shared.Helpers;
using Xunit;
using MealImportModel = MealWiz.Shared.Features.MealImport.Models.MealImport;

namespace MealWiz.Shared.Tests.Features.MealImport.UnitTests.State;

public class MealImportJobHelperTests
{
    [Theory]
    [InlineData("https://example.com/recipe", "https://example.com/recipe")]
    [InlineData("  http://example.com/a b  ", "http://example.com/a%20b")]
    [InlineData("https://example.com/a%20b", "https://example.com/a%20b")]
    public void TryNormalizeMealUrl_AcceptsHttpUrlsAndKeepsThemEscaped(string url, string expected)
    {
        Assert.True(MealImportJobHelper.TryNormalizeMealUrl(url, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData(false, false, MealImportEntryState.Default)]
    [InlineData(false, true, MealImportEntryState.Failed)]
    [InlineData(true, false, MealImportEntryState.InProgress)]
    [InlineData(true, true, MealImportEntryState.InProgress)]
    public void GetEntryState_InProgressWinsOverAnEarlierFailure(bool inProgress, bool latestFailed, MealImportEntryState expected)
    {
        Assert.Equal(expected, MealImportJobHelper.GetEntryState(inProgress, latestFailed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/recipe")]
    [InlineData("/relative/path")]
    public void TryNormalizeMealUrl_RejectsAnythingElse(string? url)
    {
        Assert.False(MealImportJobHelper.TryNormalizeMealUrl(url, out var normalized));
        Assert.Equal(string.Empty, normalized);
    }

    [Fact]
    public void IsStalePendingCopy_OnlyForAPendingCopyOfTheSameResolvedImport()
    {
        var id = Guid.NewGuid();
        var resolved = new MealImportModel { Id = id, Status = MealImportStatus.Failed };

        Assert.True(MealImportJobHelper.IsStalePendingCopy(new MealImportModel { Id = id, Status = MealImportStatus.Pending }, resolved));
        Assert.False(MealImportJobHelper.IsStalePendingCopy(new MealImportModel { Id = id, Status = MealImportStatus.Failed }, resolved));
        Assert.False(MealImportJobHelper.IsStalePendingCopy(new MealImportModel { Id = Guid.NewGuid(), Status = MealImportStatus.Pending }, resolved));
        Assert.False(MealImportJobHelper.IsStalePendingCopy(new MealImportModel { Id = id, Status = MealImportStatus.Pending }, new MealImportModel { Id = id }));
        Assert.False(MealImportJobHelper.IsStalePendingCopy(null, resolved));
        Assert.False(MealImportJobHelper.IsStalePendingCopy(new MealImportModel { Id = id }, null));
    }

    [Fact]
    public void EnsureUsableResult_SucceededWithoutPayload_BecomesFailed()
    {
        var row = MealImportJobHelper.EnsureUsableResult(new MealImportModel { Status = MealImportStatus.Succeeded });

        Assert.Equal(MealImportStatus.Failed, row.Status);
        Assert.Equal(ErrorMessages.GenericError, row.ErrorMessage);
    }

    [Fact]
    public void EnsureUsableResult_LeavesUsableRowsAlone()
    {
        var succeeded = new MealImportModel { Status = MealImportStatus.Succeeded, ParsedMeal = new ParsedMeal() };
        var failed = new MealImportModel { Status = MealImportStatus.Failed, ErrorMessage = "Nope" };

        Assert.Equal(MealImportStatus.Succeeded, MealImportJobHelper.EnsureUsableResult(succeeded).Status);
        Assert.Equal("Nope", MealImportJobHelper.EnsureUsableResult(failed).ErrorMessage);
    }

    [Fact]
    public void CreateLocallyFailedImport_KeepsWhatIsKnownAboutTheRow()
    {
        var jobId = Guid.NewGuid();
        var current = new MealImportModel
        {
            Id = jobId,
            CreatedBy = Guid.NewGuid(),
            SourceUrl = "https://example.com/recipe",
            CreatedAt = new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc)
        };

        var failed = MealImportJobHelper.CreateLocallyFailedImport(jobId, current, "Offline", DateTime.UtcNow);

        Assert.Equal(jobId, failed.Id);
        Assert.Equal(current.CreatedBy, failed.CreatedBy);
        Assert.Equal(current.SourceUrl, failed.SourceUrl);
        Assert.Equal(current.CreatedAt, failed.CreatedAt);
        Assert.Equal(MealImportStatus.Failed, failed.Status);
        Assert.Equal("Offline", failed.ErrorMessage);
    }

    [Fact]
    public void CreateLocallyFailedImport_WithoutACurrentRow_UsesNow()
    {
        var now = new DateTime(2026, 10, 12, 9, 0, 0, DateTimeKind.Utc);

        var failed = MealImportJobHelper.CreateLocallyFailedImport(Guid.NewGuid(), null, "Offline", now);

        Assert.Equal(now, failed.CreatedAt);
        Assert.Equal(string.Empty, failed.SourceUrl);
    }
}
