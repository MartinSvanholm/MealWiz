using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauiWASM.Tests.TestInfrastructure.E2E;

/// <summary>
/// The E2E tests' handle on the local Supabase stack: GoTrue admin calls to create and delete throwaway users, and
/// service-role PostgREST calls to seed rows and to play the Functions app's part in the import journey. Same
/// approach as MEA-19's LocalSupabaseFixture in MealWiz.Shared.Tests, kept separate because test projects don't
/// reference each other.
/// <para>
/// Credentials come from SUPABASE_URL / SUPABASE_SERVICE_ROLE_KEY / SUPABASE_ANON_KEY if set (CI), otherwise from
/// <c>supabase status -o env</c> run at the repo root.
/// </para>
/// </summary>
public sealed class E2ESupabase
{
    // Created by supabase/seed.sql together with the system meals, so finding it means the seed ran.
    private static readonly Guid SeededUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly HttpClient _http;

    private E2ESupabase(string url, string serviceRoleKey, string anonKey)
    {
        Url = url;
        AnonKey = anonKey;
        _http = new HttpClient
        {
            BaseAddress = new Uri($"{url.TrimEnd('/')}/"),
            Timeout = TimeSpan.FromSeconds(10)
        };
        _http.DefaultRequestHeaders.Add("apikey", serviceRoleKey);
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", serviceRoleKey);
    }

    /// <summary>The API URL the app is pointed at.</summary>
    public string Url { get; }

    /// <summary>The public key the app is given, like the publishable key it ships with.</summary>
    public string AnonKey { get; }

    /// <summary>
    /// Connects to the local stack, or returns why it can't (no CLI, stack not running, seed not applied).
    /// </summary>
    public static async Task<(E2ESupabase? Supabase, string? Unavailable)> ConnectAsync()
    {
        var url = Environment.GetEnvironmentVariable("SUPABASE_URL");
        var serviceRoleKey = Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY");
        var anonKey = Environment.GetEnvironmentVariable("SUPABASE_ANON_KEY");

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(serviceRoleKey) || string.IsNullOrWhiteSpace(anonKey))
        {
            var (status, error) = ReadSupabaseStatus();
            if (status is null) return (null, error);

            if (!status.TryGetValue("API_URL", out url)
                || !status.TryGetValue("SERVICE_ROLE_KEY", out serviceRoleKey)
                || !status.TryGetValue("ANON_KEY", out anonKey))
            {
                return (null, "`supabase status -o env` didn't include API_URL, SERVICE_ROLE_KEY and ANON_KEY.");
            }
        }

        var supabase = new E2ESupabase(url, serviceRoleKey, anonKey);

        try
        {
            using var response = await supabase._http.GetAsync($"auth/v1/admin/users/{SeededUserId}");
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (null, $"Seed user {SeededUserId} is missing. Run `supabase db reset` to apply supabase/seed.sql.");
            if (!response.IsSuccessStatusCode)
                return (null, $"Local Supabase at {url} returned {(int)response.StatusCode} when checking the seed user.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return (null, $"Local Supabase not reachable at {url}: {e.Message}");
        }

        return (supabase, null);
    }

    /// <summary>Creates an email-confirmed throwaway user. Delete it with <see cref="DeleteUserAsync"/>.</summary>
    public async Task<E2EUser> CreateUserAsync()
    {
        var email = $"e2e-{Guid.NewGuid():N}@mealwiz.test";
        var password = Guid.NewGuid().ToString("N");

        using var response = await _http.PostAsJsonAsync("auth/v1/admin/users", new CreateUserRequest(email, password, EmailConfirm: true));
        await EnsureSuccessAsync(response, $"Creating test user {email}");

        var created = await response.Content.ReadFromJsonAsync<IdRow<Guid>>()
            ?? throw new InvalidOperationException($"Creating test user {email} returned no user.");

        return new E2EUser(created.Id, email, password);
    }

    /// <summary>
    /// The link a password reset email would hold, made by the admin API without sending an email. Opening it in the
    /// browser goes through GoTrue, which redirects to <paramref name="redirectTo"/> with the recovery session.
    /// </summary>
    public async Task<string> GenerateRecoveryLinkAsync(E2EUser user, string redirectTo)
    {
        using var response = await _http.PostAsJsonAsync("auth/v1/admin/generate_link", new GenerateLinkRequest("recovery", user.Email, redirectTo));
        await EnsureSuccessAsync(response, $"Generating a reset link for {user.Email}");

        var link = await response.Content.ReadFromJsonAsync<GeneratedLink>()
            ?? throw new InvalidOperationException($"Generating a reset link for {user.Email} returned no link.");
        return link.ActionLink;
    }

    /// <summary>
    /// The link in the email GoTrue sent to <paramref name="address"/> for a pending email change, read from the local
    /// Mailpit ([inbucket] port in supabase/config.toml). The admin API's <c>generate_link</c> can't stand in: its
    /// change links confirm the whole change on the first click.
    /// </summary>
    public async Task<string> ReadEmailChangeLinkAsync(string address)
    {
        using var mailpit = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:54324/"), Timeout = TimeSpan.FromSeconds(10) };
        var link = new System.Text.RegularExpressions.Regex(@"https?://\S+/auth/v1/verify\?\S+");
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var search = await mailpit.GetFromJsonAsync<JsonElement>($"api/v1/search?query={Uri.EscapeDataString($"to:\"{address}\"")}");
            foreach (var summary in search.GetProperty("messages").EnumerateArray())
            {
                var message = await mailpit.GetFromJsonAsync<JsonElement>($"api/v1/message/{summary.GetProperty("ID").GetString()}");
                var sentToIt = message.GetProperty("To").EnumerateArray().Any(t => t.GetProperty("Address").GetString() == address);
                var match = link.Match(message.GetProperty("Text").GetString() ?? string.Empty);
                if (sentToIt && match.Success)
                    return match.Value;
            }

            await Task.Delay(200);
        }

        throw new InvalidOperationException($"No email change link for {address} arrived in Mailpit.");
    }

    /// <summary>Deletes the user. Every row it owns goes with it, since the created_by foreign keys cascade.</summary>
    public async Task DeleteUserAsync(E2EUser user)
    {
        using var response = await _http.DeleteAsync($"auth/v1/admin/users/{user.Id}");
    }

    /// <summary>Inserts a regular meal and its ingredients owned by <paramref name="owner"/>, bypassing the UI.</summary>
    public async Task<long> InsertMealAsync(E2EUser owner, string name, params SeedIngredient[] ingredients)
    {
        var now = DateTimeOffset.UtcNow;

        var meals = await InsertAsync<IdRow<long>>("meals", new
        {
            name,
            recipe = "Step one.\nStep two.",
            created_by = owner.Id,
            created_at = now
        });
        var mealId = meals.Single().Id;

        if (ingredients.Length > 0)
        {
            await InsertAsync<IdRow<long>>("ingredients", ingredients.Select(i => new
            {
                name = i.Name,
                amount = i.Amount,
                show_on_grocery_list = i.ShowOnGroceryList,
                fk_meal = mealId,
                created_by = owner.Id,
                created_at = now
            }).ToArray());
        }

        return mealId;
    }

    /// <summary>
    /// Plans <paramref name="mealId"/> on <paramref name="date"/> (yyyy-MM-dd) in <paramref name="owner"/>'s plan for that
    /// week, creating the plan the first time. Only plan days that are still empty: a week has one meal per day and one
    /// plan. Returns the new meal_plans-meals row id.
    /// </summary>
    public async Task<long> InsertPlannedMealAsync(E2EUser owner, long mealId, string date)
    {
        var day = DateTime.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var monday = day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
        var startDate = monday.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var now = DateTimeOffset.UtcNow;

        var plans = await _http.GetFromJsonAsync<IdRow<long>[]>(
            $"rest/v1/meal_plans?created_by=eq.{owner.Id}&start_date=eq.{startDate}&select=id") ?? [];
        var planId = plans.Length > 0
            ? plans[0].Id
            : (await InsertAsync<IdRow<long>>("meal_plans", new
            {
                start_date = startDate,
                end_date = monday.AddDays(6).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                created_by = owner.Id,
                created_at = now
            })).Single().Id;

        var rows = await InsertAsync<IdRow<long>>("meal_plans-meals", new
        {
            meal_date = date,
            fk_meal = mealId,
            fk_meal_plan = planId,
            created_by = owner.Id,
            created_at = now
        });
        return rows.Single().Id;
    }

    /// <summary>
    /// Resolves a pending meal_imports row the way MealWiz.Functions' ScrapeRecipeHandler does: a service-role
    /// update that only applies while the row is still pending. Realtime then pushes it to the app.
    /// </summary>
    public async Task ResolveMealImportAsync(Guid jobId, string status, object? parsedMeal = null, string? errorMessage = null,
        string? errorCode = null)
    {
        var body = JsonContent.Create(new
        {
            status,
            parsed_meal = parsedMeal,
            error_code = errorCode,
            error_message = errorMessage,
            updated_at = DateTimeOffset.UtcNow
        });

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"rest/v1/meal_imports?id=eq.{jobId}&status=eq.pending") { Content = body };
        request.Headers.Add("Prefer", "return=representation");

        using var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response, $"Resolving meal import {jobId}");

        var rows = await response.Content.ReadFromJsonAsync<JsonElement[]>() ?? [];
        if (rows.Length != 1)
            throw new InvalidOperationException($"Meal import {jobId} was not pending, so it wasn't resolved.");
    }

    /// <summary>The row's status as stored, e.g. to check that the app failed it itself.</summary>
    public async Task<string?> GetMealImportStatusAsync(Guid jobId)
    {
        var rows = await _http.GetFromJsonAsync<StatusRow[]>($"rest/v1/meal_imports?id=eq.{jobId}&select=status") ?? [];
        return rows.SingleOrDefault()?.Status;
    }

    /// <summary>A picture job's stored picture paths and language, or null when there is no such row.</summary>
    public async Task<(string[] Paths, string? Language)?> GetMealImportPicturesAsync(Guid jobId)
    {
        var rows = await _http.GetFromJsonAsync<PictureJobRow[]>(
            $"rest/v1/meal_imports?id=eq.{jobId}&select=picture_paths,language") ?? [];
        var row = rows.SingleOrDefault();
        return row is null ? null : (row.PicturePaths ?? [], row.Language);
    }

    /// <summary>How many meal_imports rows the user has.</summary>
    public async Task<int> CountMealImportsAsync(Guid userId)
    {
        var rows = await _http.GetFromJsonAsync<JsonElement[]>($"rest/v1/meal_imports?created_by=eq.{userId}&select=id") ?? [];
        return rows.Length;
    }

    /// <summary>
    /// Removes uploaded pictures through the Storage API (deleting a user doesn't remove their objects, and SQL
    /// must not be used for it).
    /// </summary>
    public async Task DeleteStoredPicturesAsync(IEnumerable<string> paths)
    {
        var list = paths.ToArray();
        if (list.Length == 0) return;

        using var request = new HttpRequestMessage(HttpMethod.Delete, "storage/v1/object/meal-import-pictures")
        {
            Content = JsonContent.Create(new { prefixes = list })
        };
        using var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response, "Deleting stored meal import pictures");
    }

    private sealed record PictureJobRow(
        [property: System.Text.Json.Serialization.JsonPropertyName("picture_paths")] string[]? PicturePaths,
        [property: System.Text.Json.Serialization.JsonPropertyName("language")] string? Language);

    private async Task<T[]> InsertAsync<T>(string table, object rows)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"rest/v1/{table}") { Content = JsonContent.Create(rows) };
        request.Headers.Add("Prefer", "return=representation");

        using var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response, $"Inserting into {table}");

        return await response.Content.ReadFromJsonAsync<T[]>() ?? [];
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string what)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"{what} returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
    }

    private static (Dictionary<string, string>? Status, string? Error) ReadSupabaseStatus()
    {
        var repoRoot = RepoPaths.FindRepoRoot();
        if (repoRoot is null)
            return (null, "Couldn't find the repo root (a directory containing supabase/config.toml).");

        var startInfo = new ProcessStartInfo("supabase", "status -o env")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception)
        {
            process = null;
        }

        if (process is null)
        {
            return (null, "Local Supabase not available: couldn't run the Supabase CLI. Set SUPABASE_URL, " +
                          "SUPABASE_SERVICE_ROLE_KEY and SUPABASE_ANON_KEY, or install the CLI.");
        }

        using (process)
        {
            var stderr = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            _ = stderr.Result;

            if (!process.WaitForExit(TimeSpan.FromSeconds(30)) || process.ExitCode != 0)
                return (null, "Local Supabase not running: `supabase status` failed. Start it with `supabase start`.");

            var values = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split('=', 2))
                .Where(parts => parts.Length == 2)
                .ToDictionary(parts => parts[0], parts => parts[1].Trim('"'));

            return (values, null);
        }
    }

    private sealed record CreateUserRequest(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("email_confirm")] bool EmailConfirm);

    private sealed record GenerateLinkRequest(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("redirect_to")] string RedirectTo);

    private sealed record GeneratedLink([property: JsonPropertyName("action_link")] string ActionLink);

    private sealed record IdRow<T>([property: JsonPropertyName("id")] T Id);

    private sealed record StatusRow([property: JsonPropertyName("status")] string Status);
}

/// <summary>A throwaway user, signed in through the app's own login form.</summary>
public sealed record E2EUser(Guid Id, string Email, string Password);

public sealed record SeedIngredient(string Name, string Amount, bool ShowOnGroceryList = true);
