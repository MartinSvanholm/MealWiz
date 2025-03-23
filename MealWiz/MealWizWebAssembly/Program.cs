using Blazored.LocalStorage;
using MealWizFeatures.Helpers;
using MealWizFeatures.Services.Authentication;
using MealWizWebAssembly;
using MealWizWebAssembly.Providers;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Supabase;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddMudServices();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MealWizFeatures.Main.Layout).Assembly));
ResultHelper.SetDefaultCatchHandler();
builder.Services.RegisterScopedServices();

string supabaseUrl = builder.Configuration["Supabase:url"];
string supabaseKey = builder.Configuration["Supabase:key"];
builder.Services.AddScoped(provider =>
{
    var supabaseClient = new Client(supabaseUrl, supabaseKey, new SupabaseOptions
    {
        AutoRefreshToken = true,
        SessionHandler = new CustomSupabaseSessionHandler(provider.GetRequiredService<ISyncLocalStorageService>()),
    });

    supabaseClient.Auth.AddStateChangedListener((sender, stateChanged) =>
    {
        provider.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();
    });

    supabaseClient.InitializeAsync().Wait();

    return supabaseClient;
});

builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>(
    provider => new CustomAuthStateProvider(provider.GetRequiredService<Client>())
);
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();