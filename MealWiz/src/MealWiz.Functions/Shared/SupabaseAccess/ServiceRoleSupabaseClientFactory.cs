using Microsoft.Extensions.Configuration;
using Supabase;

namespace MealWiz.Functions.Shared.SupabaseAccess;

public interface IServiceRoleSupabaseClientFactory
{
    Task<Client> CreateAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Builds a Supabase client authenticated with the service-role key (bypasses RLS — this project
/// is the only writer of meal_imports.status/parsed_meal/error_message, see the migration). Unlike
/// the browser client (MauiWASM/Program.cs), this is a short-lived, one-shot, server-side client:
/// no session persistence, token refresh, or Realtime connection.
/// </summary>
public class ServiceRoleSupabaseClientFactory(IConfiguration configuration) : IServiceRoleSupabaseClientFactory
{
    public async Task<Client> CreateAsync(CancellationToken cancellationToken)
    {
        var url = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url is not configured.");
        var serviceRoleKey = configuration["Supabase:ServiceRoleKey"]
            ?? throw new InvalidOperationException("Supabase:ServiceRoleKey is not configured.");

        var client = new Client(url, serviceRoleKey, new SupabaseOptions
        {
            AutoRefreshToken = false,
            AutoConnectRealtime = false
        });

        cancellationToken.ThrowIfCancellationRequested();
        await client.InitializeAsync();

        return client;
    }
}
