using Blazored.LocalStorage.Serialization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using System.Text.Json;

namespace MealWizWeb.Providers;

public class CustomSupabaseSessionHandler(IJSRuntime localStorage) : IGotrueSessionPersistence<Session>
{
    private const string SessionKey = "SUPABASE_SESSION";

    public void DestroySession()
    {
        localStorage.InvokeVoidAsync("localStorage.removeItem", SessionKey);
    }

    public Session LoadSession()
    {
        var test = localStorage.InvokeAsync<string>("localStorage.getItem", SessionKey);
        return JsonSerializer.Deserialize<Session>(test.Result);
    }

    public void SaveSession(Session session)
    {
        localStorage.InvokeVoidAsync("localStorage.setItem", SessionKey, JsonSerializer.Serialize(session));
    }
}