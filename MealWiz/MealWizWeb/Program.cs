using Features.Services.DrawerStateContainer;
using MealWizFeatures.Services.Authentication;
using MealWizFeatures.Services.DrawerStateContainer;
using MealWizWeb.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddMudServices();
        builder.Services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();

        builder.Services.AddSingleton<AuthenticationStateProvider, CustomAuthStateProvider>();

        builder.Services.AddAuthorizationCore();

        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var solutionDirectory = TryGetSolutionDirectory();

        builder.Configuration.AddJsonFile(Path.Combine(solutionDirectory.FullName, $"appsettings.{environment}.json"), optional: true, reloadOnChange: true);

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();


        app.UseAntiforgery();

        app.MapStaticAssets();
        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(MealWizFeatures._Imports).Assembly)
            .AllowAnonymous();

        app.Run();
    }

    private static DirectoryInfo TryGetSolutionDirectory()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }

        return directory;
    }
}