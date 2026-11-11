using Newtonsoft.Json;
using Supabase.Gotrue.Interfaces;

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
            string? sessionJson = Task.Run(async () => await SecureStorage.GetAsync(SESSION_KEY)).GetAwaiter().GetResult();

            if (string.IsNullOrEmpty(sessionJson))
            {
                return null;
            }

            // The SDK's Session uses Newtonsoft attributes. Unreadable JSON (corrupt, or written by the old
            // System.Text.Json format) is dropped: the user starts signed out instead of the app failing.
            try
            {
                return JsonConvert.DeserializeObject<Supabase.Gotrue.Session>(sessionJson);
            }
            catch (JsonException)
            {
                SecureStorage.Remove(SESSION_KEY);
                return null;
            }
        }

        public void SaveSession(Supabase.Gotrue.Session session)
        {
            var sessionJson = JsonConvert.SerializeObject(session);
            // Wait for the write: a refreshed token that isn't saved yet is lost if the app is killed right after.
            Task.Run(async () => await SecureStorage.SetAsync(SESSION_KEY, sessionJson)).GetAwaiter().GetResult();
        }
    }
}
