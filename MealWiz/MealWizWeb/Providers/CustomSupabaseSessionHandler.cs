using Blazored.LocalStorage;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace MealWizWeb.Providers;

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