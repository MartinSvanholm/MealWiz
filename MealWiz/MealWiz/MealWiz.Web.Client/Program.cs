using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Services.DrawerStateContainer;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using Supabase;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddMudServices();
builder.Services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();
builder.Services.AddScoped<IMealsStateContainer, MealsStateContainer>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MealWiz.Shared._Imports).Assembly));

string supabaseUrl = builder.Configuration["Supabase:url"];
string supabaseKey = builder.Configuration["Supabase:key"];
builder.Services.AddScoped(provider =>
{
    var client = new Client(supabaseUrl, supabaseKey, new SupabaseOptions
    {
        AutoRefreshToken = true,
        AutoConnectRealtime = true
    });

    return client;
});

await builder.Build().RunAsync();