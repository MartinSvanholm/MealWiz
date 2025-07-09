using MealWiz.Providers;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Services.Authentication;
using MealWiz.Shared.Services.DrawerStateContainer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Supabase;
using System.Reflection;

namespace MealWiz
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
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

            builder.Services.AddMudServices();
            builder.Services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();
            builder.Services.AddScoped<IMealsStateContainer, MealsStateContainer>();
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MealWiz.Shared._Imports).Assembly));
            builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
            builder.Services.AddAuthorizationCore();

            string supabaseUrl = builder.Configuration["Supabase:url"];
            string supabaseKey = builder.Configuration["Supabase:key"];
            builder.Services.AddScoped(provider =>
            {
                var client = new Client(supabaseUrl, supabaseKey, new SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true,
                    SessionHandler = new CustomSupabaseSessionProvider()
                });

                var initTask = Task.Run(client.InitializeAsync);
                initTask.Wait();

                client.Auth.LoadSession();

                return client;
            });

            return builder.Build();
        }
    }
}