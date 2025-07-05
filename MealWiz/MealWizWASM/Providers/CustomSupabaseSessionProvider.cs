using Blazored.LocalStorage;
using Newtonsoft.Json;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;

namespace MealWizWASM.Providers
{
    public class CustomSupabaseSessionProvider() : IGotrueSessionPersistence<Session>
    {
        private const string SessionKey = "SUPABASE_SESSION";

        public void DestroySession()
        {
        }

        public Session LoadSession()
        {
            return new Session();
        }

        public void SaveSession(Session session)
        {
        }
    }
}
