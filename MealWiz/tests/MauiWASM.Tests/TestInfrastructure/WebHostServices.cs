using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace MauiWASM.Tests.TestInfrastructure;

/// <summary>
/// What WebAssemblyHostBuilder.CreateDefault registers before Program.cs runs, faked so the app's container can be
/// built outside the browser. Everything else must come from AddMealWizWeb.
/// </summary>
public static class WebHostServices
{
    public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            // Port 1 refuses connections immediately, so the client's background connect never reaches anything.
            ["Supabase:url"] = "http://127.0.0.1:1",
            ["Supabase:key"] = "sb_publishable_test",
            ["MealImportFunction:url"] = "http://127.0.0.1:1",
            ["MealImportFunction:key"] = "",
        })
        .Build();

    public static IServiceCollection AddFakeWebHostServices(this IServiceCollection services)
    {
        var jsRuntime = new FakeJsInProcessRuntime();
        services.AddSingleton<IJSRuntime>(jsRuntime);
        services.AddSingleton<IJSInProcessRuntime>(jsRuntime);
        services.AddSingleton<NavigationManager, FakeNavigationManager>();
        services.AddSingleton(Configuration);
        services.AddLogging();
        return services;
    }

    private sealed class FakeNavigationManager : NavigationManager
    {
        public FakeNavigationManager() => Initialize("http://localhost/", "http://localhost/");
    }
}
