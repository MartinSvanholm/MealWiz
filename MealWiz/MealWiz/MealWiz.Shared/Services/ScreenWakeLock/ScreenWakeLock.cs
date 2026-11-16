using Microsoft.JSInterop;

namespace MealWiz.Shared.Services.ScreenWakeLock;

public sealed class ScreenWakeLock(IJSRuntime js) : IScreenWakeLock, IAsyncDisposable
{
    private IJSObjectReference? module;

    public async ValueTask<bool> RequestAsync()
    {
        try
        {
            var jsModule = await GetModuleAsync();
            return await jsModule.InvokeAsync<bool>("request");
        }
        catch (Exception)
        {
            // No JS, a module that fails to load or a refused lock must never break the page.
            return false;
        }
    }

    public async ValueTask ReleaseAsync()
    {
        if (module is null) return;

        try
        {
            await module.InvokeVoidAsync("release");
        }
        catch (Exception)
        {
            // The page is gone, and so is its lock.
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync() =>
        module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/MealWiz.Shared/js/screenWakeLock.js");

    public async ValueTask DisposeAsync()
    {
        if (module is null) return;
        try { await module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
}
