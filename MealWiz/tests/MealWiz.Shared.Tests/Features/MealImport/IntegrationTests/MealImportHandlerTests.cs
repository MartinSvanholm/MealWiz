using MealWiz.Shared.Features.MealImport.CreateMealImport;
using MealWiz.Shared.Features.MealImport.GetLatestMealImport;
using MealWiz.Shared.Features.MealImport.GetMealImport;
using MealWiz.Shared.Features.MealImport.MarkMealImportFailed;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Tests.TestInfrastructure;
using Supabase;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealImport.IntegrationTests;

/// <summary>
/// Runs the MealImport handlers as a freshly signed-in user. Only the import Function (service role) can mark a row
/// succeeded, so these cover the client's side: creating the pending row, reading it, and failing it.
/// </summary>
[Trait("Category", "Integration")]
public class MealImportHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private const string SourceUrl = "https://example.com/recipes/pancakes";

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateMealImport_InsertsAPendingRowWithTheClientsId()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var id = Guid.NewGuid();

        var created = ResultAssert.Ok(await CreateAsync(supabase.Client, id));

        Assert.Equal(id, created.Id);
        var row = await GetRowAsync(supabase.Client, id);
        Assert.NotNull(row);
        Assert.Equal(SourceUrl, row.SourceUrl);
        Assert.Equal(MealImportStatusExtensions.PendingValue, row.Status);
        Assert.Equal(supabase.UserId, row.CreatedBy);
        Assert.Null(row.ParsedMeal);
        Assert.Null(row.ErrorMessage);
    }

    [Fact]
    public async Task CreateMealImport_SignedOut_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var signedOut = await supabase.CreateSignedOutClientAsync();

        var result = await CreateAsync(signedOut, Guid.NewGuid());

        ResultAssert.Failed(result, ErrorMessages.NotSignedIn);
    }

    [Fact]
    public async Task GetMealImport_ReturnsTheRow()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var created = ResultAssert.Ok(await CreateAsync(supabase.Client, Guid.NewGuid()));

        var found = ResultAssert.Ok(await GetAsync(created.Id));

        Assert.Equal(created.Id, found.Id);
        Assert.Equal(SourceUrl, found.SourceUrl);
        Assert.Equal(MealImportStatus.Pending, found.Status);
        Assert.False(found.IsResolved);
    }

    [Fact]
    public async Task GetMealImport_UnknownId_Fails()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);

        var result = await GetAsync(Guid.NewGuid());

        ResultAssert.Failed(result, "The meal import could not be found.");
    }

    [Fact]
    public async Task GetLatestMealImport_ReturnsTheNewestImport()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        // A user of its own, so imports from the other tests in the class can't be newer.
        var user = await supabase.CreateSignedInUserAsync();
        ResultAssert.Ok(await CreateAsync(user.Client, Guid.NewGuid()));
        var newest = ResultAssert.Ok(await CreateAsync(user.Client, Guid.NewGuid()));

        var latest = ResultAssert.Ok(await GetLatestAsync(user.Client));

        Assert.Equal(newest.Id, latest?.Id);
    }

    [Fact]
    public async Task GetLatestMealImport_UserWithoutImports_ReturnsNull()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        // The other user has imports in another test, but RLS keeps them from this one.
        var user = await supabase.CreateSignedInUserAsync();
        var otherUser = await supabase.GetOtherUserAsync();
        ResultAssert.Ok(await CreateAsync(otherUser.Client, Guid.NewGuid()));

        var latest = ResultAssert.Ok(await GetLatestAsync(user.Client));

        Assert.Null(latest);
    }

    [Fact]
    public async Task MarkMealImportFailed_OwnPendingImport_MarksItFailed()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var created = ResultAssert.Ok(await CreateAsync(supabase.Client, Guid.NewGuid()));
        var before = DateTime.UtcNow;

        var updated = ResultAssert.Ok(await MarkFailedAsync(created.Id, "Couldn't reach the import service."));

        Assert.NotNull(updated);
        Assert.Equal(MealImportStatus.Failed, updated.Status);
        Assert.Equal("Couldn't reach the import service.", updated.ErrorMessage);
        Assert.Equal(supabase.UserId, updated.UpdatedBy);
        TimeAssert.CloseTo(before, updated.UpdatedAt);
        var row = await GetRowAsync(supabase.Client, created.Id);
        Assert.Equal(MealImportStatusExtensions.FailedValue, row?.Status);
    }

    [Fact]
    public async Task MarkMealImportFailed_AlreadyResolvedImport_ReturnsNullAndKeepsTheFirstResult()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var created = ResultAssert.Ok(await CreateAsync(supabase.Client, Guid.NewGuid()));
        Assert.NotNull(ResultAssert.Ok(await MarkFailedAsync(created.Id, "First")));

        var second = ResultAssert.Ok(await MarkFailedAsync(created.Id, "Second"));

        // Only pending rows match, so a row that's already resolved is left as it is.
        Assert.Null(second);
        var row = await GetRowAsync(supabase.Client, created.Id);
        Assert.Equal("First", row?.ErrorMessage);
    }

    [Fact]
    public async Task MarkMealImportFailed_AnotherUsersImport_ReturnsNullAndLeavesItPending()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var otherUser = await supabase.GetOtherUserAsync();
        var others = ResultAssert.Ok(await CreateAsync(otherUser.Client, Guid.NewGuid()));

        var result = ResultAssert.Ok(await MarkFailedAsync(others.Id, "Not yours"));

        Assert.Null(result);
        var row = await GetRowAsync(otherUser.Client, others.Id);
        Assert.Equal(MealImportStatusExtensions.PendingValue, row?.Status);
        Assert.Null(row?.ErrorMessage);
    }

    private static Task<FluentResults.Result<Shared.Features.MealImport.Models.MealImport>> CreateAsync(Client client, Guid id) =>
        new CreateMealImport.Handler(client).Handle(new CreateMealImport.Command(id, SourceUrl), CancellationToken);

    private Task<FluentResults.Result<Shared.Features.MealImport.Models.MealImport>> GetAsync(Guid id) =>
        new GetMealImport.Handler(supabase.Client).Handle(new GetMealImport.Query(id), CancellationToken);

    private static Task<FluentResults.Result<Shared.Features.MealImport.Models.MealImport?>> GetLatestAsync(Client client) =>
        new GetLatestMealImport.Handler(client).Handle(new GetLatestMealImport.Query(), CancellationToken);

    private Task<FluentResults.Result<Shared.Features.MealImport.Models.MealImport?>> MarkFailedAsync(Guid id, string errorMessage) =>
        new MarkMealImportFailed.Handler(supabase.Client).Handle(new MarkMealImportFailed.Command(id, errorMessage), CancellationToken);

    private static async Task<MealImportDb?> GetRowAsync(Client client, Guid id)
    {
        var response = await client.From<MealImportDb>().Where(x => x.Id == id).Get(CancellationToken);
        return response.Models.SingleOrDefault();
    }
}
