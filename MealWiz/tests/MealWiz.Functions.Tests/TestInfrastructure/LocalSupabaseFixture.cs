using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using FluentResults;
using MealWiz.Functions.Shared.Models;
using MealWiz.Functions.Shared.SupabaseAccess;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MealWiz.Functions.Tests.TestInfrastructure;

/// <summary>
/// Connects integration tests to a local Supabase stack. Credentials come from the
/// SUPABASE_URL / SUPABASE_SERVICE_ROLE_KEY environment variables if set, otherwise from
/// <c>supabase status -o env</c> run at the repo root. If the stack can't be reached, the fixture
/// records a <see cref="SkipReason"/> instead of failing, so tests skip and <c>dotnet test</c> stays
/// green on machines without Docker. With REQUIRE_SUPABASE=true (set in CI) it throws instead, so a
/// broken stack fails the tests rather than silently skipping them. Every row created through
/// <see cref="InsertPendingRowAsync"/> is deleted when the fixture is disposed.
/// </summary>
public sealed class LocalSupabaseFixture : IAsyncLifetime
{
    // Created by supabase/seed.sql. meal_imports.created_by has a foreign key to auth.users.
    public static readonly Guid SeededUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private const string UrlVariable = "SUPABASE_URL";
    private const string ServiceRoleKeyVariable = "SUPABASE_SERVICE_ROLE_KEY";
    private const string RequireSupabaseVariable = "REQUIRE_SUPABASE";

    private readonly ConcurrentBag<Guid> _createdRowIds = [];

    public string? SkipReason { get; private set; }
    public bool IsAvailable => SkipReason is null;
    public IConfiguration Configuration { get; private set; } = new ConfigurationBuilder().Build();
    public Supabase.Client Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var credentialsResult = ResolveCredentials();
        if (credentialsResult.IsFailed)
        {
            MarkUnavailable(credentialsResult.Errors);
            return;
        }

        var (url, serviceRoleKey) = credentialsResult.Value;

        var seedUserResult = await CheckSeededUserExistsAsync(url, serviceRoleKey);
        if (seedUserResult.IsFailed)
        {
            MarkUnavailable(seedUserResult.Errors);
            return;
        }

        Configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Supabase:Url"] = url,
                ["Supabase:ServiceRoleKey"] = serviceRoleKey
            })
            .Build();

        Client = await new ServiceRoleSupabaseClientFactory(Configuration).CreateAsync(CancellationToken.None);
    }

    public async Task<MealImportDb> InsertPendingRowAsync(string sourceUrl)
    {
        var response = await Client.From<MealImportDb>().Insert(new MealImportDb
        {
            CreatedBy = SeededUserId,
            SourceUrl = sourceUrl,
            Status = RecipeImportStatus.Pending.ToDbValue(),
            CreatedAt = DateTime.UtcNow
        });

        var row = response.Model ?? throw new InvalidOperationException("Inserting a meal_imports row returned no row.");
        _createdRowIds.Add(row.Id);
        return row;
    }

    public Task<MealImportDb?> GetRowAsync(Guid id) =>
        Client.From<MealImportDb>().Where(x => x.Id == id).Single();

    public async ValueTask DisposeAsync()
    {
        if (!IsAvailable) return;

        foreach (var id in _createdRowIds)
        {
            await Client.From<MealImportDb>().Where(x => x.Id == id).Delete();
        }
    }

    private static Result<(string Url, string ServiceRoleKey)> ResolveCredentials()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        var serviceRoleKey = Environment.GetEnvironmentVariable(ServiceRoleKeyVariable);

        if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(serviceRoleKey))
        {
            return Result.Ok((url, serviceRoleKey));
        }

        var statusResult = ReadSupabaseStatus();
        if (statusResult.IsFailed)
        {
            return Result.Fail<(string, string)>(statusResult.Errors);
        }

        var status = statusResult.Value;
        if (!status.TryGetValue("API_URL", out var statusUrl) || !status.TryGetValue("SERVICE_ROLE_KEY", out var statusKey))
        {
            return Result.Fail("`supabase status -o env` didn't include API_URL and SERVICE_ROLE_KEY.");
        }

        return Result.Ok((statusUrl, statusKey));
    }

    private static Result<Dictionary<string, string>> ReadSupabaseStatus()
    {
        var repoRoot = FindRepoRoot();
        if (repoRoot is null)
        {
            return Result.Fail("Couldn't find the repo root (a directory containing supabase/config.toml).");
        }

        var startInfo = new ProcessStartInfo("supabase", "status -o env")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var processResult = Result.Try(() => Process.Start(startInfo)!);
        if (processResult.IsFailed)
        {
            return Result.Fail($"Local Supabase not available: couldn't run the Supabase CLI. Set {UrlVariable} and {ServiceRoleKeyVariable}, or install the CLI.");
        }

        using var process = processResult.Value;
        var output = process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();

        if (!process.WaitForExit(TimeSpan.FromSeconds(30)) || process.ExitCode != 0)
        {
            return Result.Fail("Local Supabase not running: `supabase status` failed. Start it with `supabase start`.");
        }

        var values = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1].Trim('"'));

        return Result.Ok(values);
    }

    private static async Task<Result> CheckSeededUserExistsAsync(string url, string serviceRoleKey)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{url.TrimEnd('/')}/auth/v1/admin/users/{SeededUserId}");
        request.Headers.Add("apikey", serviceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);

        var responseResult = await Result.Try(() => http.SendAsync(request));
        if (responseResult.IsFailed)
        {
            return Result.Fail($"Local Supabase not reachable at {url}: {Describe(responseResult.Errors)}");
        }

        using var response = responseResult.Value;

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Fail($"Seed user {SeededUserId} is missing. Run `supabase db reset` to apply supabase/seed.sql.");
        }

        return response.IsSuccessStatusCode
            ? Result.Ok()
            : Result.Fail($"Local Supabase at {url} returned {(int)response.StatusCode} when checking the seed user.");
    }

    private static string? FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "supabase", "config.toml")))
            {
                return directory.FullName;
            }
        }

        return null;
    }

    private void MarkUnavailable(IReadOnlyList<IError> errors)
    {
        var reason = Describe(errors);

        if (string.Equals(Environment.GetEnvironmentVariable(RequireSupabaseVariable), "true", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{RequireSupabaseVariable} is set, but local Supabase isn't available: {reason}");
        }

        SkipReason = reason;
    }

    private static string Describe(IReadOnlyList<IError> errors) => string.Join("; ", errors.Select(e => e.Message));
}
