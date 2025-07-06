using Supabase.Gotrue.Interfaces;
using System.Text.Json;

namespace MealWiz.Providers
{
    public class CustomSupabaseSessionProvider : IGotrueSessionPersistence<Supabase.Gotrue.Session>
    {
        private const string SESSION_KEY = "supabase_session";
        public void DestroySession()
        {
            SecureStorage.Remove(SESSION_KEY);
        }

        public Supabase.Gotrue.Session? LoadSession()
        {
            var sessionTask = Task.Run(async () => await SecureStorage.GetAsync(SESSION_KEY));
            string sessionJson = sessionTask.Result;

            if (string.IsNullOrEmpty(sessionJson))
            {
                return new Supabase.Gotrue.Session();
            }
            
            var session = JsonSerializer.Deserialize<Supabase.Gotrue.Session>(sessionJson);
            return session;
        }

        public void SaveSession(Supabase.Gotrue.Session session)
        {
            var sessionJson = JsonSerializer.Serialize(session);
            Task.Run(async () => await SecureStorage.SetAsync(SESSION_KEY, sessionJson));
        }
    }
}
