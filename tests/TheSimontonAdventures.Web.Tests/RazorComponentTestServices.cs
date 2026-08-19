using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace TheSimontonAdventures.Web.Tests;

internal sealed class StaticTestJavaScriptRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        ValueTask.FromResult(default(TValue)!);

    public ValueTask<TValue> InvokeAsync<TValue>(
        string identifier,
        CancellationToken cancellationToken,
        object?[]? args) => ValueTask.FromResult(default(TValue)!);
}

internal sealed class StaticTestNavigationManager : NavigationManager
{
    public StaticTestNavigationManager()
    {
        Initialize("https://localhost/", "https://localhost/");
    }
}
