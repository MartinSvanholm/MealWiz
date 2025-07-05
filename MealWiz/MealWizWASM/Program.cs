using MealWizWASM;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using MealWizWASM.Providers;
using Supabase;
using MudBlazor.Services;
using Features.Services.DrawerStateContainer;
using MealWizFeatures.Services.DrawerStateContainer;
using MealWizFeatures.Features.Meals.State;
using Supabase.Gotrue;
using Client = Supabase.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<AuthenticationStateProvider, MealWizFeatures.Services.Authentication.CustomAuthStateProvider>();
builder.Services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();
builder.Services.AddScoped<IMealsStateContainer, MealsStateContainer>();

builder.Services.AddMudServices();
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MealWizFeatures.Main.Layout).Assembly));

ServiceProvider serviceProvider = builder.Services.BuildServiceProvider();

string supabaseUrl = builder.Configuration["Supabase:url"];
string supabaseKey = builder.Configuration["Supabase:key"];
builder.Services.AddScoped(provider =>
{
    var client = new Client(supabaseUrl, supabaseKey, new SupabaseOptions
    {
        AutoRefreshToken = false,
        AutoConnectRealtime = true,
        SessionHandler = new CustomSupabaseSessionProvider()
    });

    client.Auth.AddStateChangedListener((client, authstate) =>
    {
        var authStateProvider = provider.GetRequiredService<AuthenticationStateProvider>();

        if (authstate == Constants.AuthState.SignedIn
            || authstate == Constants.AuthState.SignedOut
            || authstate == Constants.AuthState.TokenRefreshed)
        {
            authStateProvider.GetAuthenticationStateAsync();
        }
    });

    client.InitializeAsync();

    return client;
});

await builder.Build().RunAsync();