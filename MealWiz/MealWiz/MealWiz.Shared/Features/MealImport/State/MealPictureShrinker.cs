using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace MealWiz.Shared.Features.MealImport.State;

/// <param name="Ok">False when the browser couldn't decode the file.</param>
/// <param name="Bytes">The shrunk JPEG.</param>
/// <param name="PreviewUrl">An object URL for the thumbnail; give it back to <see cref="IMealPictureShrinker.RevokePreviewAsync"/>.</param>
public record ShrunkPicture(bool Ok, byte[]? Bytes, string? PreviewUrl);

/// <summary>
/// Shrinks the pictures picked in a file input before they are uploaded. The only code in Shared that calls the
/// <c>js/mealPictures.js</c> module. Blazor's own image resize hangs on a file the browser can't decode, so the
/// module uses <c>createImageBitmap</c> and a canvas, which fail cleanly.
/// </summary>
public interface IMealPictureShrinker
{
    /// <summary>Shrinks the first <paramref name="maxCount"/> files chosen in <paramref name="input"/> and clears the input.</summary>
    Task<IReadOnlyList<ShrunkPicture>> ShrinkSelectedAsync(ElementReference input, int maxCount);

    ValueTask RevokePreviewAsync(string previewUrl);
}

public sealed class MealPictureShrinker(IJSRuntime js) : IMealPictureShrinker, IAsyncDisposable
{
    /// <summary>The long edge of a shrunk picture, in pixels.</summary>
    public const int MaxEdge = 2500;

    /// <summary>The JPEG quality, 0 to 1.</summary>
    public const double Quality = 0.8;

    private IJSObjectReference? module;

    public async Task<IReadOnlyList<ShrunkPicture>> ShrinkSelectedAsync(ElementReference input, int maxCount)
    {
        var jsModule = await GetModuleAsync();
        var results = await jsModule.InvokeAsync<List<JsShrunkPicture>>("shrinkSelectedFiles", input, maxCount, MaxEdge, Quality);

        return results
            .Select(r => r.Ok && r.Bytes is not null
                ? new ShrunkPicture(true, Convert.FromBase64String(r.Bytes), r.PreviewUrl)
                : new ShrunkPicture(false, null, null))
            .ToList();
    }

    public async ValueTask RevokePreviewAsync(string previewUrl)
    {
        try
        {
            var jsModule = await GetModuleAsync();
            await jsModule.InvokeVoidAsync("revokePreview", previewUrl);
        }
        catch (JSDisconnectedException)
        {
            // The page is gone, and so are its object URLs.
        }
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync() =>
        module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/MealWiz.Shared/js/mealPictures.js");

    public async ValueTask DisposeAsync()
    {
        if (module is null) return;
        try { await module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }

    // The module returns the bytes as base64: Blazor passes a Uint8Array only as a top-level result.
    private sealed record JsShrunkPicture(bool Ok, string? Bytes, string? PreviewUrl);
}
