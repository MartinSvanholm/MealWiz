using FluentResults;
using MealWiz.Shared.Features.RecipeImport.Models;
using Supabase;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;

namespace MealWiz.Shared.Features.RecipeImport.State;

/// <summary>
/// Watches a single meal_imports row over Supabase Realtime. Kept behind an interface so the state
/// container's job lifecycle can be tested without a live Realtime socket.
/// </summary>
public interface IRecipeImportRealtimeSubscriber
{
    /// <summary>
    /// Completes once the channel join is acknowledged, so no update written after this returns is missed.
    /// Dispose the returned handle to unsubscribe.
    /// </summary>
    Task<Result<IAsyncDisposable>> Subscribe(Guid recipeImportId, Action<Models.RecipeImport> onUpdate);
}

public class RecipeImportRealtimeSubscriber(Client supabaseClient) : IRecipeImportRealtimeSubscriber
{
    public async Task<Result<IAsyncDisposable>> Subscribe(Guid recipeImportId, Action<Models.RecipeImport> onUpdate)
    {
        return await Result.Try(async Task<IAsyncDisposable> () =>
        {
            var realtime = supabaseClient.Realtime;

            // The WASM host fires InitializeAsync without awaiting it, so the socket may not be up yet.
            if (realtime.Socket?.IsConnected != true)
                await realtime.ConnectAsync();

            // Realtime enforces RLS on postgres_changes using this JWT.
            var accessToken = supabaseClient.Auth.CurrentSession?.AccessToken;
            if (!string.IsNullOrEmpty(accessToken))
                realtime.SetAuth(accessToken);

            var channel = realtime.Channel("realtime", "public", "meal_imports", "id", recipeImportId.ToString());

            IRealtimeChannel.PostgresChangesHandler handler = (_, change) =>
            {
                var recipeImportDb = change.Model<RecipeImportDb>();
                if (recipeImportDb is not null)
                    onUpdate(new Models.RecipeImport(recipeImportDb));
            };
            channel.AddPostgresChangeHandler(PostgresChangesOptions.ListenType.Updates, handler);

            await channel.Subscribe();

            return new Subscription(realtime, channel, handler);
        });
    }

    private sealed class Subscription(
        IRealtimeClient<Supabase.Realtime.RealtimeSocket, Supabase.Realtime.RealtimeChannel> realtime,
        Supabase.Realtime.RealtimeChannel channel,
        IRealtimeChannel.PostgresChangesHandler handler) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            channel.RemovePostgresChangeHandler(PostgresChangesOptions.ListenType.Updates, handler);

            // Remove() unsubscribes (leaves the channel) if it's still joined. A failed leave (e.g. the
            // socket already dropped) isn't worth surfacing — the job is resolved either way.
            Result.Try(() => realtime.Remove(channel));

            return ValueTask.CompletedTask;
        }
    }
}
