using Blazored.LocalStorage;
using MauiWASM.Provider;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Services.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Supabase;

namespace MauiWASM;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Every service the web app registers on top of what WebAssemblyHostBuilder provides. Lives here rather than in
    /// Program.cs so MauiWASM.Tests can build the same container and catch a missing registration.
    /// </summary>
    public static IServiceCollection AddMealWizWeb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBlazoredLocalStorage();
        services.AddMudServices();
        services.RegisterScopedServices();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MealWiz.Shared._Imports).Assembly));
        services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
        services.AddAuthorizationCore();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped(_ => new HttpClient());
        services.AddSingleton(new MealImportFunctionOptions(
            configuration["MealImportFunction:url"] ?? string.Empty,
            configuration["MealImportFunction:key"]));

        string supabaseUrl = configuration["Supabase:url"] ?? string.Empty;
        string? supabaseKey = configuration["Supabase:key"];

        services.AddScoped(provider =>
        {
            var client = new Client(supabaseUrl, supabaseKey, new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true,
                SessionHandler = new CustomSupabaseSessionProvider(provider.GetRequiredService<ISyncLocalStorageService>())
            });

            client.InitializeAsync();
            client.Auth.LoadSession();

            return client;
        });

        return services;
    }
}
