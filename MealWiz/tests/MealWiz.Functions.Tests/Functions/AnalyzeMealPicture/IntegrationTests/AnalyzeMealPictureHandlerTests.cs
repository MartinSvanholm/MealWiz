using FluentResults;
using MealWiz.Functions.Functions.AnalyzeMealPicture;
using MealWiz.Functions.Shared.Models;
using MealWiz.Functions.Shared.SupabaseAccess;
using MealWiz.Functions.Tests.TestInfrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.AnalyzeMealPicture.IntegrationTests;

/// <summary>
/// <see cref="AnalyzeMealPictureHandler"/> end to end against local Supabase and Storage, with a fake analyzer (no test
/// calls the real AI service). The daily-limit tests count the seeded user's picture jobs, so every test that adds
/// picture rows is in one collection and they run one after another.
/// </summary>
[Trait("Category", "Integration")]
[Collection("PictureJobs")]
public class AnalyzeMealPictureHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    [Fact]
    public async Task WrittenRecipe_WritesSucceededAndNotGuessed()
    {
        var (row, analyzer) = await RunAsync(await NewJobAsync(pictures: 1), analyzer: new FakeAnalyzer(Payload("Pasta", guessed: false)));

        Assert.Equal(RecipeImportStatus.Succeeded, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.NotNull(row.UpdatedAt);
        Assert.Null(row.ErrorCode);
        Assert.Equal("Pasta", row.ParsedMeal!.Name);
        Assert.False(row.ParsedMeal.IsGuessed);
        Assert.Equal(1, analyzer.Calls.Count);
    }

    [Fact]
    public async Task Dish_WritesAGuessedMeal()
    {
        var (row, _) = await RunAsync(await NewJobAsync(pictures: 1), analyzer: new FakeAnalyzer(Payload("Lasagne", guessed: true)));

        Assert.Equal(RecipeImportStatus.Succeeded, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.True(row.ParsedMeal!.IsGuessed);
    }

    [Fact]
    public async Task ThreePictures_AreSentInOneRequestInOrder_WithTheLanguageAndHint()
    {
        var job = await NewJobAsync(pictures: 3, language: "da", hint: "it's lasagne");
        var analyzer = new FakeAnalyzer(Payload("Lasagne", guessed: false));

        await RunAsync(job, analyzer: analyzer);

        var call = Assert.Single(analyzer.Calls);
        Assert.Equal(new byte[][] { [0xFF, 0xD8, 0], [0xFF, 0xD8, 1], [0xFF, 0xD8, 2] }, call.Pictures.ToArray());
        Assert.Equal("da", call.Language);
        Assert.Equal("it's lasagne", call.Hint);
    }

    [Theory]
    [InlineData("other-user")]
    [InlineData("traversal")]
    [InlineData("missing")]
    public async Task BadOrMissingPicture_WritesPictureUnreadableWithoutCallingTheAnalyzer(string kind)
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var jobId = Guid.NewGuid();
        var path = kind switch
        {
            "other-user" => $"{Guid.NewGuid():D}/{jobId:D}/0.jpg",
            "traversal" => $"{LocalSupabaseFixture.SeededUserId:D}/../{Guid.NewGuid():D}/0.jpg",
            _ => $"{LocalSupabaseFixture.SeededUserId:D}/{jobId:D}/0.jpg"
        };
        var job = await supabase.InsertPictureRowAsync([path]);
        var analyzer = new FakeAnalyzer(Payload("Pasta", guessed: false));

        var (row, _) = await RunAsync(job, analyzer: analyzer);

        AssertFailed(row, RecipeImportErrorCode.PictureUnreadable);
        Assert.Empty(analyzer.Calls);
    }

    [Theory]
    [InlineData(RecipeImportErrorCode.NoMealFound)]
    [InlineData(RecipeImportErrorCode.PictureUnreadable)]
    [InlineData(RecipeImportErrorCode.AiUnavailable)]
    [InlineData(RecipeImportErrorCode.TimedOut)]
    public async Task AnalyzerFailure_IsWrittenWithItsCode(RecipeImportErrorCode code)
    {
        var analyzer = new FakeAnalyzer(Result.Fail<MealImportPayload>(new PictureImportError(code, "detail for " + code)));

        var (row, _) = await RunAsync(await NewJobAsync(pictures: 1), analyzer: analyzer);

        AssertFailed(row, code);
        Assert.Equal("detail for " + code, row.ErrorMessage);
    }

    [Fact]
    public async Task AnalyzerFailureWithoutACode_IsWrittenAsUnknown()
    {
        var analyzer = new FakeAnalyzer(Result.Fail<MealImportPayload>("boom"));

        var (row, _) = await RunAsync(await NewJobAsync(pictures: 1), analyzer: analyzer);

        AssertFailed(row, RecipeImportErrorCode.Unknown);
    }

    [Theory]
    [InlineData(-6 * 60)]
    [InlineData(+3 * 60)]
    public async Task StaleOrFutureDatedRow_WritesTimedOutWithoutCallingTheAnalyzer(int createdSecondsFromNow)
    {
        var job = await NewJobAsync(pictures: 1, createdAt: DateTime.UtcNow.AddSeconds(createdSecondsFromNow));
        var analyzer = new FakeAnalyzer(Payload("Pasta", guessed: false));

        var (row, _) = await RunAsync(job, analyzer: analyzer);

        AssertFailed(row, RecipeImportErrorCode.TimedOut);
        Assert.Empty(analyzer.Calls);
    }

    [Fact]
    public async Task LinkJob_IsLeftUntouched()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var link = await supabase.InsertPendingRowAsync("https://example.test/recipe");
        var analyzer = new FakeAnalyzer(Payload("Pasta", guessed: false));

        var (row, _) = await RunAsync(link, analyzer: analyzer);

        Assert.Equal(RecipeImportStatus.Pending, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.Null(row.UpdatedAt);
        Assert.Empty(analyzer.Calls);
    }

    [Theory]
    [InlineData(RecipeImportStatus.Succeeded)]
    [InlineData(RecipeImportStatus.Failed)]
    public async Task ResolvedPictureJob_IsLeftUntouched(RecipeImportStatus status)
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var job = await supabase.InsertPictureRowAsync([$"{LocalSupabaseFixture.SeededUserId:D}/x/0.jpg"], status: status);
        var analyzer = new FakeAnalyzer(Payload("Pasta", guessed: false));

        var (row, _) = await RunAsync(job, analyzer: analyzer);

        Assert.Equal(status, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.Null(row.ParsedMeal);
        Assert.Empty(analyzer.Calls);
    }

    // The client can fail the row while the analysis runs; the first resolution wins.
    [Fact]
    public async Task RowResolvedDuringTheAnalysis_KeepsItsFirstResolution()
    {
        var job = await NewJobAsync(pictures: 1);
        var analyzer = new FakeAnalyzer(async () =>
        {
            await supabase.Client.From<MealImportDb>().Where(x => x.Id == job.Id)
                .Set(x => x.Status, RecipeImportStatus.Failed.ToDbValue())
                .Set(x => x.ErrorCode!, RecipeImportErrorCode.Unreachable.ToDbValue())
                .Update();
            return Result.Ok(Payload("Pasta", guessed: false).Value);
        });

        var (row, _) = await RunAsync(job, analyzer: analyzer);

        AssertFailed(row, RecipeImportErrorCode.Unreachable);
        Assert.Null(row.ParsedMeal);
    }

    [Fact]
    public async Task TwoConcurrentCalls_AnalyseOnce()
    {
        var job = await NewJobAsync(pictures: 1);
        var analyzer = new FakeAnalyzer(async () =>
        {
            await Task.Delay(300);
            return Result.Ok(Payload("Pasta", guessed: false).Value);
        });
        var handler = CreateHandler(analyzer);

        var results = await Task.WhenAll(
            handler.HandleAsync(new AnalyzeMealPictureRequest(job.Id), TestContext.Current.CancellationToken),
            handler.HandleAsync(new AnalyzeMealPictureRequest(job.Id), TestContext.Current.CancellationToken));

        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.Single(analyzer.Calls);
        var row = await supabase.GetRowAsync(job.Id);
        Assert.Equal(RecipeImportStatus.Succeeded, RecipeImportStatusExtensions.FromDbValue(row!.Status));
    }

    [Fact]
    public async Task UnknownJobId_FailsWithoutCreatingARow()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var unknownId = Guid.NewGuid();

        var result = await CreateHandler(new FakeAnalyzer(Payload("Pasta", guessed: false)))
            .HandleAsync(new AnalyzeMealPictureRequest(unknownId), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailed);
        Assert.Null(await supabase.GetRowAsync(unknownId));
    }

    [Fact]
    public async Task RejectedServiceKey_FailsAndLeavesTheRowPending()
    {
        var job = await NewJobAsync(pictures: 1);
        var rejectedKeyConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Url"] = supabase.Configuration["Supabase:Url"],
            ["Supabase:ServiceRoleKey"] = "sb_secret_not-a-real-key"
        }).Build();
        var analyzer = new FakeAnalyzer(Payload("Pasta", guessed: false));

        var result = await CreateHandler(analyzer, clientFactory: new ServiceRoleSupabaseClientFactory(rejectedKeyConfiguration))
            .HandleAsync(new AnalyzeMealPictureRequest(job.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailed);
        var row = await supabase.GetRowAsync(job.Id);
        Assert.Equal(RecipeImportStatus.Pending, RecipeImportStatusExtensions.FromDbValue(row!.Status));
        Assert.Null(row.UpdatedAt);
        Assert.Empty(analyzer.Calls);
    }

    private async Task<MealImportDb> NewJobAsync(
        int pictures, string language = "en", string? hint = null, DateTime? createdAt = null)
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? string.Empty);
        var jobId = Guid.NewGuid();
        var paths = new List<string>();
        for (var i = 0; i < pictures; i++)
            paths.Add(await supabase.UploadPictureAsync([.. Jpeg.Take(2), (byte)i], jobId, i));
        return await supabase.InsertPictureRowAsync(paths, language, hint, createdAt);
    }

    private async Task<(MealImportDb Row, FakeAnalyzer Analyzer)> RunAsync(
        MealImportDb job, FakeAnalyzer analyzer, int? dailyLimit = null)
    {
        var result = await CreateHandler(analyzer, dailyLimit: dailyLimit)
            .HandleAsync(new AnalyzeMealPictureRequest(job.Id), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(e => e.Message)));
        var row = await supabase.GetRowAsync(job.Id);
        Assert.NotNull(row);
        return (row, analyzer);
    }

    private AnalyzeMealPictureHandler CreateHandler(
        FakeAnalyzer analyzer, IServiceRoleSupabaseClientFactory? clientFactory = null, int? dailyLimit = null) => new(
        clientFactory ?? new ServiceRoleSupabaseClientFactory(supabase.Configuration),
        analyzer,
        // The seeded user is shared by every integration test, so the default limit is high.
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["MealPictureImport:DailyLimit"] = (dailyLimit ?? 1000).ToString()
        }).Build(),
        NullLogger<AnalyzeMealPictureHandler>.Instance);

    private static Result<MealImportPayload> Payload(string name, bool guessed) =>
        Result.Ok(new MealImportPayload { Name = name, IsGuessed = guessed, Recipe = "Cook.", Ingredients = [new() { Name = "salt", Amount = "1 tsk" }] });

    private static void AssertFailed(MealImportDb row, RecipeImportErrorCode code)
    {
        Assert.Equal(RecipeImportStatus.Failed, RecipeImportStatusExtensions.FromDbValue(row.Status));
        Assert.Null(row.ParsedMeal);
        Assert.Equal(code, RecipeImportErrorCodeExtensions.FromDbValue(row.ErrorCode));
        Assert.NotNull(row.UpdatedAt);
    }

    private sealed class FakeAnalyzer : IMealPictureAnalyzer
    {
        private readonly Func<Task<Result<MealImportPayload>>> _answer;

        public FakeAnalyzer(Result<MealImportPayload> result) => _answer = () => Task.FromResult(result);

        public FakeAnalyzer(Func<Task<Result<MealImportPayload>>> answer) => _answer = answer;

        public List<MealPictureAnalysisRequest> Calls { get; } = [];

        public Task<Result<MealImportPayload>> AnalyzeAsync(MealPictureAnalysisRequest request, CancellationToken cancellationToken)
        {
            lock (Calls) Calls.Add(request);
            return _answer();
        }
    }
}
