using Blazored.LocalStorage;
using MealWiz.Providers;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Localization;
using MealWiz.Shared.Services.Authentication;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using MudBlazor.Services;
using Supabase;
using Supabase.Gotrue.Interfaces;
using Client = Supabase.Client;
using Session = Supabase.Gotrue.Session;
using System.Globalization;
using System.Reflection;

namespace MealWiz
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // The language is picked once, before the BlazorWebView exists: the stored choice (Preferences, same
            // key as the web app's local storage), else the OS language.
            var deviceCulture = CultureInfo.CurrentUICulture;
            CultureResolver.Apply(CultureResolver.Resolve(
                Preferences.Default.Get<string?>(SupportedLanguages.StorageKey, null), deviceCulture));

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif
            var a = Assembly.GetExecutingAssembly();
            string appSettings = $"{a.GetName().Name}.Resources.appsettings.Development.json";
            using var stream = a.GetManifestResourceStream(appSettings);

            var config = new ConfigurationBuilder()
                .AddJsonStream(stream)
                .Build();
            builder.Configuration.AddConfiguration(config);

            builder.Services.AddBlazoredLocalStorageAsSingleton();
            builder.Services.AddMudServices();
            builder.Services.RegisterScopedServices();
            builder.Services.AddScoped<ILanguagePreference>(provider =>
                new PreferencesLanguagePreference(provider.GetRequiredService<NavigationManager>(), deviceCulture));
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MealWiz.Shared._Imports).Assembly));
            builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
            builder.Services.AddAuthorizationCore();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton(new MealImportFunctionOptions(
                builder.Configuration["MealImportFunction:url"] ?? string.Empty,
                builder.Configuration["MealImportFunction:key"]));

            // An email's link can't open this app, so it opens the web app (decision Q7).
            builder.Services.AddSingleton(new MealWiz.Shared.Features.Authentication.EmailLinks.EmailLinkOptions(builder.Configuration["WebApp:url"]));
            builder.Services.AddSingleton(new MealWiz.Shared.Features.Authentication.SupabaseAuthSettings(
                builder.Configuration["Supabase:url"] ?? string.Empty, builder.Configuration["Supabase:key"] ?? string.Empty));

            string supabaseUrl = builder.Configuration["Supabase:url"];
            string supabaseKey = builder.Configuration["Supabase:key"];
            // One client, persistence and session keeper for the whole app. The SDK's own refresh is off:
            // SessionKeeper loads, refreshes and schedules (it is started by the SessionGate).
            builder.Services.AddSingleton<IGotrueSessionPersistence<Session>>(_ => new CustomSupabaseSessionProvider());
            builder.Services.AddSingleton(provider => new Client(supabaseUrl, supabaseKey, new SupabaseOptions
            {
                AutoRefreshToken = false,
                AutoConnectRealtime = false,
                SessionHandler = provider.GetRequiredService<IGotrueSessionPersistence<Session>>()
            }));
            builder.Services.AddSingleton<SessionKeeper>();

            ResultHelper.SetDefaultCatchHandler();

            return builder.Build();
        }
    }
}