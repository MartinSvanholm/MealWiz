using MealWiz.Functions.Functions.AnalyzeMealPicture;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.AnalyzeMealPicture.UnitTests;

public class PictureJobRulesTests
{
    private static readonly Guid Owner = Guid.Parse("AAAAAAAA-0000-0000-0000-0000000000A1");
    private const string OwnerFolder = "aaaaaaaa-0000-0000-0000-0000000000a1";

    [Theory]
    [InlineData(null, 20)]
    [InlineData("", 20)]
    [InlineData("abc", 20)]
    [InlineData("-3", 20)]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData("1000", 1000)]
    public void DailyLimit_ReadsTheSetting(string? value, int expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["MealPictureImport:DailyLimit"] = value }).Build();

        Assert.Equal(expected, PictureJobRules.DailyLimit(configuration));
    }

    [Theory]
    [InlineData(-299, true)]
    [InlineData(-301, false)]
    [InlineData(59, true)]
    [InlineData(61, false)]
    public void IsFresh_AllowsFiveMinutesOldAndAMinuteAhead(int createdSecondsFromNow, bool expected)
    {
        var now = new DateTime(2026, 11, 12, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(expected, PictureJobRules.IsFresh(now.AddSeconds(createdSecondsFromNow), now));
    }

    [Fact]
    public void Paths_UnderTheOwnersFolder_AreValid()
    {
        Assert.True(PictureJobRules.AreValidPaths(Owner, [$"{OwnerFolder}/job/0.jpg"]));
        Assert.True(PictureJobRules.AreValidPaths(Owner, [$"{OwnerFolder}/a/0.jpg", $"{OwnerFolder}/b/1.jpg", $"{OwnerFolder}/c/2.jpg"]));
    }

    [Theory]
    [InlineData("bbbbbbbb-0000-0000-0000-0000000000b2/job/0.jpg")]
    [InlineData("aaaaaaaa-0000-0000-0000-0000000000a1/../bbbbbbbb-0000-0000-0000-0000000000b2/job/0.jpg")]
    [InlineData("aaaaaaaa-0000-0000-0000-0000000000a1/./0.jpg")]
    [InlineData("aaaaaaaa-0000-0000-0000-0000000000a1//0.jpg")]
    [InlineData("aaaaaaaa-0000-0000-0000-0000000000a1\\job\\0.jpg")]
    [InlineData("AAAAAAAA-0000-0000-0000-0000000000A1/job/0.jpg")]
    [InlineData("aaaaaaaa-0000-0000-0000-0000000000a1")]
    [InlineData("aaaaaaaa-0000-0000-0000-0000000000a1/")]
    [InlineData("")]
    public void BadPath_IsInvalid(string path) => Assert.False(PictureJobRules.AreValidPaths(Owner, [path]));

    [Fact]
    public void NoPathsOrTooMany_AreInvalid()
    {
        Assert.False(PictureJobRules.AreValidPaths(Owner, null));
        Assert.False(PictureJobRules.AreValidPaths(Owner, []));
        Assert.False(PictureJobRules.AreValidPaths(Owner, Enumerable.Range(0, 4).Select(i => $"{OwnerFolder}/j/{i}.jpg").ToList()));
    }
}
