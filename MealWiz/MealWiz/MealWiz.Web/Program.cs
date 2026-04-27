using Blazored.LocalStorage;
using MealWiz.Shared.Features.MealPlans.State;
using MealWiz.Shared.Features.Meals.State;
using MealWiz.Shared.Services.Authentication;
using MealWiz.Shared.Services.DrawerStateContainer;
using MealWiz.Web.Components;
using MealWiz.Web.Providers;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Supabase;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddMudServices();
builder.Services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();
builder.Services.AddScoped<IMealsStateContainer, MealsStateContainer>();
builder.Services.AddScoped<IMealPlanStateContainer, MealPlanStateContainer>();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MealWiz.Shared._Imports).Assembly));
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddBlazoredLocalStorage();

builder.Services.AddAuthorization();
builder.Services.AddAuthentication("Auth")
    .AddCookie("Auth", options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
    });
builder.Services.AddCascadingAuthenticationState();

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

    return client;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(
        typeof(MealWiz.Shared._Imports).Assembly,
        typeof(MealWiz.Web.Client._Imports).Assembly);

app.UseAuthorization();

app.Run();
