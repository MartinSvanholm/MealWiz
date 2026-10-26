using Blazored.LocalStorage;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace MauiWASM.Provider
{
    public class CustomSupabaseSessionProvider(ISyncLocalStorageService localStorage) : IGotrueSessionPersistence<Session>
    {
        private const string SessionKey = "SUPABASE_SESSION";

        public void DestroySession()
        {
            if (localStorage.ContainKey(SessionKey))
            {
                localStorage.RemoveItem(SessionKey);
            }
        }

        public Session LoadSession()
        {
            if (!localStorage.ContainKey(SessionKey))
                return new Session();

            var sessionJson = localStorage.GetItem<string>(SessionKey);

            // The client loads the session while it's being built at startup, so unreadable JSON (corrupt, or
            // written by an older format) must not throw: drop it and start signed out instead.
            try
            {
                return JsonConvert.DeserializeObject<Session>(sessionJson ?? string.Empty) ?? new Session();
            }
            catch (JsonException)
            {
                localStorage.RemoveItem(SessionKey);
                return new Session();
            }
        }

        public void SaveSession(Session session)
        {
            var sessionJson = JsonConvert.SerializeObject(session);
            localStorage.SetItem(SessionKey, sessionJson);
        }
    }
}
