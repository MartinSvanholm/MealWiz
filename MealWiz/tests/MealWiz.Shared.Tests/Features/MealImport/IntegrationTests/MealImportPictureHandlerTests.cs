using MealWiz.Shared.Features.MealImport;
using MealWiz.Shared.Features.MealImport.CreatePictureMealImport;
using MealWiz.Shared.Features.MealImport.DeleteMealImportPictures;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Features.MealImport.UploadMealImportPictures;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Supabase;
using Xunit;
using MealImportModel = MealWiz.Shared.Features.MealImport.Models.MealImport;

namespace MealWiz.Shared.Tests.Features.MealImport.IntegrationTests;

/// <summary>
/// The picture handlers as a freshly signed-in user, against local Supabase with Storage: upload and delete in the
/// private pictures bucket, and inserting the pending picture job. The Function's side is covered by MealWiz.Functions.Tests.
/// </summary>
[Trait("Category", "Integration")]
public class MealImportPictureHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private static readonly Microsoft.Extensions.Localization.IStringLocalizer<MealImportResource> Localizer =
        TestLocalizer.For<MealImportResource>();

    [Fact]
    public async Task Upload_StoresThePicturesInOrderUnderTheUsersFolder_AndTheUserCanReadThem()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var jobId = Guid.NewGuid();

        var paths = ResultAssert.Ok(await UploadAsync(user.Client, jobId, [[0xFF, 0xD8, 0], [0xFF, 0xD8, 1], [0xFF, 0xD8, 2]]));

        Assert.Equal(
            [$"{user.Id:D}/{jobId:D}/0.jpg", $"{user.Id:D}/{jobId:D}/1.jpg", $"{user.Id:D}/{jobId:D}/2.jpg"], paths);
        Assert.All(paths, path => Assert.Equal(path, path.ToLowerInvariant()));
        for (var i = 0; i < paths.Count; i++)
            Assert.Equal(new byte[] { 0xFF, 0xD8, (byte)i }, await DownloadAsync(user.Client, paths[i]));

        await DeleteAsync(user.Client, paths);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public async Task Upload_ZeroOrMoreThanThreePictures_Fails(int count)
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();

        var result = await UploadAsync(user.Client, Guid.NewGuid(), Enumerable.Range(0, count).Select(_ => Jpeg).ToList());

        ResultAssert.Failed(result, Localizer["Import_PictureUploadFailed"].Value);
    }

    [Fact]
    public async Task Upload_SignedOut_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await UploadAsync(signedOut, Guid.NewGuid(), [Jpeg]);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }

    [Fact]
    public async Task Upload_WhenALaterPictureFails_RemovesTheEarlierOnes()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var jobId = Guid.NewGuid();
        var tooLarge = new byte[5 * 1024 * 1024 + 1]; // over the bucket's 5 MB limit

        var result = await UploadAsync(user.Client, jobId, [Jpeg, tooLarge]);

        Assert.True(result.IsFailed);
        var firstPath = MealImportPictures.PathFor(user.Id, jobId, 0);
        await Assert.ThrowsAnyAsync<Exception>(() => DownloadAsync(user.Client, firstPath));
    }

    [Fact]
    public async Task Delete_RemovesThePictures_AndAnEmptyListIsFine()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var user = await supabase.CreateSignedInUserAsync();
        var paths = ResultAssert.Ok(await UploadAsync(user.Client, Guid.NewGuid(), [Jpeg, Jpeg]));

        ResultAssert.Ok(await DeleteAsync(user.Client, paths));
        ResultAssert.Ok(await DeleteAsync(user.Client, []));

        foreach (var path in paths)
            await Assert.ThrowsAnyAsync<Exception>(() => DownloadAsync(user.Client, path));
    }

    [Fact]
    public async Task Delete_AnotherUsersPicture_LeavesItInPlace()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var owner = await supabase.CreateSignedInUserAsync();
        var other = await supabase.CreateSignedInUserAsync();
        var paths = ResultAssert.Ok(await UploadAsync(owner.Client, Guid.NewGuid(), [Jpeg]));

        await DeleteAsync(other.Client, paths);

        Assert.Equal(Jpeg, await DownloadAsync(owner.Client, paths[0]));
        await DeleteAsync(owner.Client, paths);
    }

    [Fact]
    public async Task CreatePictureMealImport_InsertsAPendingPictureJob()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var id = Guid.NewGuid();
        string[] paths = [$"{supabase.UserId:D}/{id:D}/0.jpg", $"{supabase.UserId:D}/{id:D}/1.jpg"];

        var created = ResultAssert.Ok(await CreateAsync(supabase.Client, id, paths, "da", "  it's lasagne "));

        Assert.True(created.IsPictureImport);
        Assert.Null(created.SourceUrl);
        var row = await GetRowAsync(supabase.Client, id);
        Assert.NotNull(row);
        Assert.Null(row.SourceUrl);
        Assert.Equal(paths, row.PicturePaths);
        Assert.Equal("da", row.Language);
        Assert.Equal("it's lasagne", row.Hint);
        Assert.Equal(MealImportStatusExtensions.PendingValue, row.Status);
        Assert.Equal(supabase.UserId, row.CreatedBy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreatePictureMealImport_BlankHint_IsStoredAsNull(string? hint)
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var id = Guid.NewGuid();

        ResultAssert.Ok(await CreateAsync(supabase.Client, id, [$"{supabase.UserId:D}/{id:D}/0.jpg"], "en", hint));

        Assert.Null((await GetRowAsync(supabase.Client, id))!.Hint);
    }

    [Fact]
    public async Task CreatePictureMealImport_OverLongHint_FailsWithoutInserting()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var id = Guid.NewGuid();

        var result = await CreateAsync(supabase.Client, id, [$"{supabase.UserId:D}/{id:D}/0.jpg"], "en", new string('x', 201));

        Assert.True(result.IsFailed);
        Assert.Null(await GetRowAsync(supabase.Client, id));
    }

    [Fact]
    public async Task CreatePictureMealImport_SignedOut_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await CreateAsync(signedOut, Guid.NewGuid(), ["u/j/0.jpg"], "en", null);

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }

    private static Task<FluentResults.Result<IReadOnlyList<string>>> UploadAsync(Client client, Guid jobId, IReadOnlyList<byte[]> pictures) =>
        new UploadMealImportPictures.Handler(client, Localizer)
            .Handle(new UploadMealImportPictures.Command(jobId, pictures), CancellationToken);

    private static Task<FluentResults.Result> DeleteAsync(Client client, IReadOnlyList<string> paths) =>
        new DeleteMealImportPictures.Handler(client).Handle(new DeleteMealImportPictures.Command(paths), CancellationToken);

    private static Task<FluentResults.Result<MealImportModel>> CreateAsync(
        Client client, Guid id, IReadOnlyList<string> paths, string language, string? hint) =>
        new CreatePictureMealImport.Handler(client, Localizer)
            .Handle(new CreatePictureMealImport.Command(id, paths, language, hint), CancellationToken);

    private static Task<byte[]> DownloadAsync(Client client, string path) =>
        client.Storage.From(MealImportPictures.BucketId).Download(path, (EventHandler<float>?)null);

    private static async Task<MealImportDb?> GetRowAsync(Client client, Guid id)
    {
        var response = await client.From<MealImportDb>().Where(x => x.Id == id).Get(CancellationToken);
        return response.Models.SingleOrDefault();
    }
}
