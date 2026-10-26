using MauiWASM;
using MealWiz.Shared.Helpers;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMealWizWeb(builder.Configuration);

ResultHelper.SetDefaultCatchHandler();

await builder.Build().RunAsync();
