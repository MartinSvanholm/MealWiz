using Microsoft.JSInterop;

namespace MauiWASM.Tests.TestInfrastructure;

/// <summary>
/// Stands in for the browser's JS runtime. Every call returns default, so Blazored's local storage reads as empty.
/// </summary>
public sealed class FakeJsInProcessRuntime : IJSInProcessRuntime
{
    public TValue Invoke<TValue>(string identifier, params object?[]? args) => default!;

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => ValueTask.FromResult<TValue>(default!);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
        ValueTask.FromResult<TValue>(default!);
}
