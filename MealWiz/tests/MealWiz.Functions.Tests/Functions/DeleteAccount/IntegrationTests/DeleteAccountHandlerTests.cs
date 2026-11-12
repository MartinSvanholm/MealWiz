using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MealWiz.Functions.Functions.DeleteAccount;
using MealWiz.Functions.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.DeleteAccount.IntegrationTests;

/// <summary>
/// The handler against the local GoTrue: throwaway users (created through the admin API, each with a meal) are
/// deleted, or kept, as the token and password dictate. Skips (with the reason) when local Supabase isn't running.
/// </summary>
[Trait("Category", "Integration")]
public class DeleteAccountHandlerTests(LocalSupabaseFixture supabase) : IClassFixture<LocalSupabaseFixture>
{
    private const string Password = "Throwaway-pw-1";

    [Fact]
    public async Task RightTokenAndPassword_DeletesTheUserAndTheirMeals_AndLeavesOthersAlone()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? "");
        using var http = NewHttp();
        var user = await CreateUserWithMealAsync(http);
        var other = await CreateUserWithMealAsync(http);

        var outcome = await NewHandler().HandleAsync(user.AccessToken, Password, TestContext.Current.CancellationToken);

        Assert.Equal(DeleteAccountOutcome.Deleted, outcome);
        Assert.False(await UserExistsAsync(http, user.Id));
        Assert.Equal(0, await MealCountAsync(http, user.Id));
        Assert.True(await UserExistsAsync(http, other.Id));
        Assert.Equal(1, await MealCountAsync(http, other.Id));

        await DeleteUserAsync(http, other.Id);
    }

    [Fact]
    public async Task WrongPassword_KeepsTheUserAndTheirMeal()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? "");
        using var http = NewHttp();
        var user = await CreateUserWithMealAsync(http);

        var outcome = await NewHandler().HandleAsync(user.AccessToken, "not-the-password", TestContext.Current.CancellationToken);

        Assert.Equal(DeleteAccountOutcome.WrongPassword, outcome);
        Assert.True(await UserExistsAsync(http, user.Id));
        Assert.Equal(1, await MealCountAsync(http, user.Id));

        await DeleteUserAsync(http, user.Id);
    }

    [Fact]
    public async Task GarbageToken_IsUnauthorized()
    {
        Assert.SkipUnless(supabase.IsAvailable, supabase.SkipReason ?? "");

        var outcome = await NewHandler().HandleAsync("not-a-jwt", Password, TestContext.Current.CancellationToken);

        Assert.Equal(DeleteAccountOutcome.Unauthorized, outcome);
    }

    private DeleteAccountHandler NewHandler()
    {
        var factory = new ServiceCollection().AddHttpClient(DeleteAccountHandler.HttpClientName).Services
            .BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        return new DeleteAccountHandler(factory, supabase.Configuration, NullLogger<DeleteAccountHandler>.Instance);
    }

    private HttpClient NewHttp()
    {
        var http = new HttpClient { BaseAddress = new Uri(supabase.Configuration["Supabase:Url"]!.TrimEnd('/') + "/") };
        var key = supabase.Configuration["Supabase:ServiceRoleKey"]!;
        http.DefaultRequestHeaders.Add("apikey", key);
        http.DefaultRequestHeaders.Authorization = new("Bearer", key);
        return http;
    }

    private static async Task<(Guid Id, string AccessToken)> CreateUserWithMealAsync(HttpClient http)
    {
        var email = $"delete-{Guid.NewGuid():N}@integration.test";
        var created = await http.PostAsJsonAsync("auth/v1/admin/users",
            new { email, password = Password, email_confirm = true }, TestContext.Current.CancellationToken);
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();

        var token = await http.PostAsJsonAsync("auth/v1/token?grant_type=password",
            new { email, password = Password }, TestContext.Current.CancellationToken);
        token.EnsureSuccessStatusCode();
        var accessToken = (await token.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken))
            .GetProperty("access_token").GetString()!;

        var meal = await http.PostAsJsonAsync("rest/v1/meals",
            new { name = "Throwaway meal", created_by = id, created_at = DateTime.UtcNow }, TestContext.Current.CancellationToken);
        meal.EnsureSuccessStatusCode();

        return (id, accessToken);
    }

    private static async Task<bool> UserExistsAsync(HttpClient http, Guid id)
    {
        var response = await http.GetAsync($"auth/v1/admin/users/{id}", TestContext.Current.CancellationToken);
        return response.StatusCode != HttpStatusCode.NotFound;
    }

    private static async Task<int> MealCountAsync(HttpClient http, Guid userId)
    {
        var rows = await http.GetFromJsonAsync<JsonElement>($"rest/v1/meals?created_by=eq.{userId}&select=id", TestContext.Current.CancellationToken);
        return rows.GetArrayLength();
    }

    private static Task DeleteUserAsync(HttpClient http, Guid id) =>
        http.DeleteAsync($"auth/v1/admin/users/{id}", TestContext.Current.CancellationToken);
}
