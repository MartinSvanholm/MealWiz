using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using FluentResults;
using Supabase;
using Xunit;

namespace MealWiz.Shared.Tests.TestInfrastructure;

/// <summary>
/// Connects integration tests to a local Supabase stack as a signed-in user, the way the WASM app does: anon key
/// plus a user session, so every query runs under RLS. Each instance (one per test class, via
/// <c>IClassFixture</c>) creates a throwaway user through the GoTrue admin API and signs it in, so test classes can
/// run in parallel without seeing each other's rows. Every user it creates is deleted on dispose, and the
/// <c>created_by</c> foreign keys cascade, so their rows go with them.
/// <para>
/// Credentials come from the SUPABASE_URL / SUPABASE_SERVICE_ROLE_KEY / SUPABASE_ANON_KEY environment variables if
/// set, otherwise from <c>supabase status -o env</c> run at the repo root. If the stack can't be reached, the fixture
/// records a <see cref="SkipReason"/> instead of failing, so tests skip and <c>dotnet test</c> stays green on
/// machines without Docker. With REQUIRE_SUPABASE=true (set in CI) it throws instead, so a broken stack fails the
/// tests rather than silently skipping them.
/// </para>
/// </summary>
public sealed class LocalSupabaseFixture : IAsyncLifetime
{
    // Created by supabase/seed.sql together with the system meals, so finding it means the seed ran.
    public static readonly Guid SeededUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private const string UrlVariable = "SUPABASE_URL";
    private const string ServiceRoleKeyVariable = "SUPABASE_SERVICE_ROLE_KEY";
    private const string AnonKeyVariable = "SUPABASE_ANON_KEY";
    private const string RequireSupabaseVariable = "REQUIRE_SUPABASE";

    // Resolved once per test run: every test class gets its own fixture, and each `supabase status` takes a while.
    private static readonly Lazy<Task<Result<Credentials>>> SharedCredentials = new(ResolveAndCheckCredentialsAsync);

    private readonly ConcurrentBag<Guid> _createdUserIds = [];
    private Credentials _credentials = null!;

    public string? SkipReason { get; private set; }
    public bool IsAvailable => SkipReason is null;

    /// <summary>The test class's own user, signed in with the anon key. Pass it to handlers as their <see cref="Client"/>.</summary>
    public Client Client => User.Client;
    public Guid UserId => User.Id;
    public TestUser User { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        var credentialsResult = await SharedCredentials.Value;
        if (credentialsResult.IsFailed)
        {
            MarkUnavailable(credentialsResult.Errors);
            return;
        }

        _credentials = credentialsResult.Value;
        User = await CreateSignedInUserAsync();
    }

    /// <summary>
    /// Creates another throwaway user and signs it in, e.g. to check that one user can't see another's rows.
    /// It is deleted with the fixture, like <see cref="User"/>.
    /// </summary>
    public async Task<TestUser> CreateSignedInUserAsync()
    {
        var email = $"shared-tests-{Guid.NewGuid():N}@mealwiz.test";
        var password = Guid.NewGuid().ToString("N");

        using var http = CreateAdminHttpClient();
        using var response = await http.PostAsJsonAsync("auth/v1/admin/users", new CreateUserRequest(email, password, EmailConfirm: true));
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Creating test user {email} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }

        var created = await response.Content.ReadFromJsonAsync<CreatedUser>()
            ?? throw new InvalidOperationException($"Creating test user {email} returned no user.");
        _createdUserIds.Add(created.Id);

        var client = new Client(_credentials.Url, _credentials.AnonKey, new SupabaseOptions
        {
            AutoRefreshToken = false,
            AutoConnectRealtime = false
        });
        await client.InitializeAsync();
        await client.Auth.SignIn(email, password);

        return new TestUser(created.Id, email, client);
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsAvailable) return;

        using var http = CreateAdminHttpClient();
        foreach (var id in _createdUserIds)
        {
            using var response = await http.DeleteAsync($"auth/v1/admin/users/{id}");
        }
    }

    private HttpClient CreateAdminHttpClient() => CreateAdminHttpClient(_credentials);

    private static HttpClient CreateAdminHttpClient(Credentials credentials)
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri($"{credentials.Url.TrimEnd('/')}/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        http.DefaultRequestHeaders.Add("apikey", credentials.ServiceRoleKey);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", credentials.ServiceRoleKey);
        return http;
    }

    private static async Task<Result<Credentials>> ResolveAndCheckCredentialsAsync()
    {
        var credentialsResult = ResolveCredentials();
        if (credentialsResult.IsFailed)
        {
            return credentialsResult;
        }

        var seedUserResult = await CheckSeededUserExistsAsync(credentialsResult.Value);
        return seedUserResult.IsFailed ? Result.Fail<Credentials>(seedUserResult.Errors) : credentialsResult;
    }

    private static Result<Credentials> ResolveCredentials()
    {
        var url = Environment.GetEnvironmentVariable(UrlVariable);
        var serviceRoleKey = Environment.GetEnvironmentVariable(ServiceRoleKeyVariable);
        var anonKey = Environment.GetEnvironmentVariable(AnonKeyVariable);

        if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(serviceRoleKey) && !string.IsNullOrWhiteSpace(anonKey))
        {
            return Result.Ok(new Credentials(url, serviceRoleKey, anonKey));
        }

        var statusResult = ReadSupabaseStatus();
        if (statusResult.IsFailed)
        {
            return Result.Fail<Credentials>(statusResult.Errors);
        }

        var status = statusResult.Value;
        if (!status.TryGetValue("API_URL", out var statusUrl)
            || !status.TryGetValue("SERVICE_ROLE_KEY", out var statusServiceRoleKey)
            || !status.TryGetValue("ANON_KEY", out var statusAnonKey))
        {
            return Result.Fail("`supabase status -o env` didn't include API_URL, SERVICE_ROLE_KEY and ANON_KEY.");
        }

        return Result.Ok(new Credentials(statusUrl, statusServiceRoleKey, statusAnonKey));
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
            return Result.Fail($"Local Supabase not available: couldn't run the Supabase CLI. Set {UrlVariable}, {ServiceRoleKeyVariable} and {AnonKeyVariable}, or install the CLI.");
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

    private static async Task<Result> CheckSeededUserExistsAsync(Credentials credentials)
    {
        using var http = CreateAdminHttpClient(credentials);

        var responseResult = await Result.Try(() => http.GetAsync($"auth/v1/admin/users/{SeededUserId}"));
        if (responseResult.IsFailed)
        {
            return Result.Fail($"Local Supabase not reachable at {credentials.Url}: {Describe(responseResult.Errors)}");
        }

        using var response = responseResult.Value;

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return Result.Fail($"Seed user {SeededUserId} is missing. Run `supabase db reset` to apply supabase/seed.sql.");
        }

        return response.IsSuccessStatusCode
            ? Result.Ok()
            : Result.Fail($"Local Supabase at {credentials.Url} returned {(int)response.StatusCode} when checking the seed user.");
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

    private sealed record Credentials(string Url, string ServiceRoleKey, string AnonKey);

    private sealed record CreateUserRequest(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("email_confirm")] bool EmailConfirm);

    private sealed record CreatedUser([property: JsonPropertyName("id")] Guid Id);
}

/// <summary>A throwaway user and a <see cref="Supabase.Client"/> signed in as it with the anon key.</summary>
public sealed record TestUser(Guid Id, string Email, Client Client);
