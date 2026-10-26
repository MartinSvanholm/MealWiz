using MealWiz.Functions.Shared.Models;
using Xunit;

namespace MealWiz.Functions.Tests.Shared.UnitTests.Models;

public class RecipeImportStatusTests
{
    [Theory]
    [InlineData(RecipeImportStatus.Pending, "pending")]
    [InlineData(RecipeImportStatus.Succeeded, "succeeded")]
    [InlineData(RecipeImportStatus.Failed, "failed")]
    public void Status_RoundTripsThroughDbValue(RecipeImportStatus status, string dbValue)
    {
        Assert.Equal(dbValue, status.ToDbValue());
        Assert.Equal(status, RecipeImportStatusExtensions.FromDbValue(dbValue));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("Succeeded")] // DB values are case-sensitive
    public void Status_UnknownDbValue_FallsBackToPending(string? dbValue)
    {
        Assert.Equal(RecipeImportStatus.Pending, RecipeImportStatusExtensions.FromDbValue(dbValue));
    }
}
