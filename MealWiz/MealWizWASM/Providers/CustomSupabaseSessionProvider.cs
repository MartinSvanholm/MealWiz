using Blazored.LocalStorage;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace MealWizWASM.Providers
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
            return JsonConvert.DeserializeObject<Session>(sessionJson);
        }

        public void SaveSession(Session session)
        {
            var sessionJson = JsonConvert.SerializeObject(session);
            localStorage.SetItem(SessionKey, sessionJson);
        }
    }
}
