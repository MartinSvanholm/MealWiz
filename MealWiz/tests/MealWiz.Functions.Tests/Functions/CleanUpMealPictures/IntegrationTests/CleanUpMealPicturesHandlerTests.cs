using MealWiz.Functions.Functions.CleanUpMealPictures;
using MealWiz.Functions.Shared.SupabaseAccess;
using MealWiz.Functions.Tests.TestInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.CleanUpMealPictures.IntegrationTests;

/// <summary>
/// The daily clean-up against local Supabase and Storage. It deletes every picture older than its cutoff, so these tests
/// share the <c>PictureJobs</c> collection with the other picture tests and pick the cutoff right after their own upload.
/// </summary>
[Trait("Category", "Integration")]
[Collection("PictureJobs")]
public class CleanUpMealPicturesHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    [Fact]
    public async Task RemovesThePicturesOlderThanTheCutoff_AndKeepsTheNewerOnes()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var older = await supabase.UploadPictureAsync(Jpeg);
        var cutoff = DateTimeOffset.UtcNow.AddMilliseconds(500);
        await Task.Delay(TimeSpan.FromSeconds(1.5), TestContext.Current.CancellationToken);
        var newer = await supabase.UploadPictureAsync(Jpeg);

        var result = await CreateHandler().HandleAsync(cutoff, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.True(result.Value >= 1);
        Assert.False(await supabase.PictureExistsAsync(older));
        Assert.True(await supabase.PictureExistsAsync(newer));
    }

    [Fact]
    public async Task NothingOlderThanTheCutoff_RemovesNothing()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var picture = await supabase.UploadPictureAsync(Jpeg);

        // Older than every picture that exists, so nothing qualifies.
        var result = await CreateHandler().HandleAsync(DateTimeOffset.UtcNow.AddYears(-1), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value);
        Assert.True(await supabase.PictureExistsAsync(picture));
    }

    [Fact]
    public async Task RejectedServiceKey_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var rejected = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Url"] = supabase.Configuration["Supabase:Url"],
            ["Supabase:ServiceRoleKey"] = "sb_secret_not-a-real-key"
        }).Build();

        var result = await CreateHandler(new ServiceRoleSupabaseClientFactory(rejected))
            .HandleAsync(DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.True(result.IsFailed);
    }

    private CleanUpMealPicturesHandler CreateHandler(IServiceRoleSupabaseClientFactory? clientFactory = null) => new(
        clientFactory ?? new ServiceRoleSupabaseClientFactory(supabase.Configuration),
        NullLogger<CleanUpMealPicturesHandler>.Instance);
}
