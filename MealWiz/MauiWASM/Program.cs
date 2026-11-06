using System.Globalization;
using MauiWASM;
using MealWiz.Shared.Helpers;
using MealWiz.Shared.Localization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMealWizWeb(builder.Configuration);

ResultHelper.SetDefaultCatchHandler();

var host = builder.Build();

// The language is picked once, before the first render: the stored choice, else the browser's language. Satellite
// assemblies (the Danish texts) load for the current culture inside RunAsync, so it must be set first. Changing it
// here needs BlazorWebAssemblyLoadAllGlobalizationData (MauiWASM.csproj). index.html sets <html lang> the same way.
CultureResolver.Apply(CultureResolver.Resolve(await ReadStoredLanguageAsync(host.Services), CultureInfo.CurrentCulture));

await host.RunAsync();

// Read raw (the key holds a plain string, not JSON) through IJSRuntime: the Blazored service is scoped.
static async Task<string?> ReadStoredLanguageAsync(IServiceProvider services)
{
    try
    {
        return await services.GetRequiredService<IJSRuntime>().InvokeAsync<string?>("localStorage.getItem", SupportedLanguages.StorageKey);
    }
    catch (JSException)
    {
        return null; // storage blocked (private mode, disabled site data): follow the browser
    }
}
