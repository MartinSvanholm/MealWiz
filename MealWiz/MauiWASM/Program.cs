using Blazored.LocalStorage;
using MauiWASM;
using MauiWASM.Provider;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Services.Authentication;
using MealWiz.Shared.Services.DrawerStateContainer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Supabase;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddBlazoredLocalStorage();
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
        SessionHandler = new CustomSupabaseSessionProvider(provider.GetRequiredService<ISyncLocalStorageService>())
    });

    client.InitializeAsync();
    client.Auth.LoadSession();

    return client;
});

await builder.Build().RunAsync();