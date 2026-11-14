using MealWiz.Shared.Localization;
using Blazored.LocalStorage;
using MauiWASM.Provider;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Services.Authentication;
using MealWiz.Shared.Services.FunctionApp;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Client = Supabase.Client;
using Session = Supabase.Gotrue.Session;

namespace MauiWASM;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Every service the web app registers on top of what WebAssemblyHostBuilder provides. Lives here rather than in
    /// Program.cs so MauiWASM.Tests can build the same container and catch a missing registration.
    /// </summary>
    public static IServiceCollection AddMealWizWeb(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddBlazoredLocalStorageAsSingleton();
        services.AddMudServices();
        services.RegisterScopedServices();
        services.AddScoped<ILanguagePreference, LocalStorageLanguagePreference>();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MealWiz.Shared._Imports).Assembly));
        services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
        services.AddAuthorizationCore();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new FunctionAppOptions(
            configuration["MealImportFunction:url"] ?? string.Empty,
            configuration["MealImportFunction:key"]));

        services.AddSingleton(new MealWiz.Shared.Features.Authentication.EmailLinks.EmailLinkOptions(null)); // links point at this app
        services.AddSingleton(new MealWiz.Shared.Features.Authentication.SupabaseAuthSettings(
            configuration["Supabase:url"] ?? string.Empty, configuration["Supabase:key"] ?? string.Empty));

        string supabaseUrl = configuration["Supabase:url"] ?? string.Empty;
        string? supabaseKey = configuration["Supabase:key"];

        // One client, one persistence and one session keeper for the whole app (the keeper outlives pages and
        // refreshes the session on a timer). The SDK's own refresh is off: SessionKeeper does it.
        services.AddSingleton<IGotrueSessionPersistence<Session>>(provider =>
            new CustomSupabaseSessionProvider(provider.GetRequiredService<ISyncLocalStorageService>()));
        services.AddSingleton(provider => new Client(supabaseUrl, supabaseKey, new SupabaseOptions
        {
            AutoRefreshToken = false,
            AutoConnectRealtime = false,
            SessionHandler = provider.GetRequiredService<IGotrueSessionPersistence<Session>>()
        }));
        services.AddSingleton<SessionKeeper>();

        return services;
    }
}
