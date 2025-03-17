using Blazored.LocalStorage;
using Microsoft.JSInterop;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using System.Text.Json;

namespace MealWizWebAssembly.Providers;

public class CustomSupabaseSessionHandler(ISyncLocalStorageService localStorage) : IGotrueSessionPersistence<Session>
{
    private const string SessionKey = "SUPABASE_SESSION";

    public void DestroySession()
    {
        localStorage.RemoveItem(SessionKey);
    }

    public Session LoadSession()
    {
        return localStorage.GetItem<Session>(SessionKey);
    }

    public void SaveSession(Session session)
    {
        localStorage.SetItem(SessionKey, session);
    }
}