using MealWiz.Functions.Shared.SupabaseAccess;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MealWiz.Functions.Tests.Shared.UnitTests.SupabaseAccess;

/// <summary>
/// The configuration checks that run before any network call. Creating a real client is covered by the
/// handler integration tests against local Supabase.
/// </summary>
public class ServiceRoleSupabaseClientFactoryTests
{
    [Fact]
    public async Task CreateAsync_MissingUrl_ThrowsWithClearMessage()
    {
        var factory = CreateFactory(new() { ["Supabase:ServiceRoleKey"] = "service-role-key" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.CreateAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Supabase:Url is not configured.", exception.Message);
    }

    [Fact]
    public async Task CreateAsync_MissingServiceRoleKey_ThrowsWithClearMessage()
    {
        var factory = CreateFactory(new() { ["Supabase:Url"] = "http://127.0.0.1:54321" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.CreateAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Supabase:ServiceRoleKey is not configured.", exception.Message);
    }

    private static ServiceRoleSupabaseClientFactory CreateFactory(Dictionary<string, string?> settings) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
}
