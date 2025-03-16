using Blazored.LocalStorage;
using Features.Services.DrawerStateContainer;
using MealWizFeatures.Services.Authentication;
using MealWizFeatures.Services.DrawerStateContainer;
using MealWizWeb.Components;
using MealWizWeb.Providers;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Supabase;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        string environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        var solutionDirectory = TryGetSolutionDirectory();

        builder.Configuration.AddJsonFile(Path.Combine(solutionDirectory.FullName, $"appsettings.{environment}.json"), optional: true, reloadOnChange: true);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddMudServices();
        builder.Services.AddScoped<IDrawerStateContainer, DrawerStateContainer>();
        builder.Services.AddBlazoredLocalStorage();

        string supabaseUrl = builder.Configuration["Supabase:url"];
        string supabaseKey = builder.Configuration["Supabase:key"];
        builder.Services.AddScoped(provider =>
        {
            return new Supabase.Client(supabaseUrl, supabaseKey, new SupabaseOptions
            {
                AutoRefreshToken = true,
                AutoConnectRealtime = true,
                SessionHandler = new CustomSupabaseSessionHandler(provider.GetRequiredService<ISyncLocalStorageService>())
            });
        });

        builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

        builder.Services.AddAuthorizationCore();

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

        Startup(app);
    }

    private static void Startup(WebApplication app)
    {
        List<Task> startupTasks = [];

        var supabaseClient = app.Services.GetRequiredService<Client>();

        startupTasks.Add(supabaseClient.InitializeAsync());

        Task.WaitAll(startupTasks);
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