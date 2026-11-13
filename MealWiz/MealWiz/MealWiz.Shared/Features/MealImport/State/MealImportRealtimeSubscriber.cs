using FluentResults;
using MealWiz.Shared.Features.MealImport.Models;
using Supabase;
using Supabase.Realtime.Interfaces;
using Supabase.Realtime.PostgresChanges;
using Supabase.Realtime.Socket;

namespace MealWiz.Shared.Features.MealImport.State;

/// <summary>
/// Watches a single meal_imports row over Supabase Realtime. Kept behind an interface so the state
/// container's job lifecycle can be tested without a live Realtime socket.
/// </summary>
public interface IMealImportRealtimeSubscriber
{
    /// <summary>
    /// Completes once Realtime has subscribed the channel as the signed-in user, so no update written after this
    /// returns is missed.
    /// Dispose the returned handle to unsubscribe.
    /// </summary>
    Task<Result<IAsyncDisposable>> Subscribe(Guid mealImportId, Action<Models.MealImport> onUpdate);
}

public class MealImportRealtimeSubscriber(Client supabaseClient) : IMealImportRealtimeSubscriber
{
    public async Task<Result<IAsyncDisposable>> Subscribe(Guid mealImportId, Action<Models.MealImport> onUpdate)
    {
        return await Result.Try(async Task<IAsyncDisposable> () =>
        {
            var realtime = supabaseClient.Realtime;

            // With no channel left the client is closing its socket after the previous job's leave. A join sent over that
            // half-closed socket never gets the user's token, so start from a socket of our own instead.
            if (realtime.Subscriptions.Count == 0 && realtime.Socket?.IsConnected == true)
                realtime.Disconnect();

            // The WASM host fires InitializeAsync without awaiting it, so the socket may not be up yet.
            if (realtime.Socket?.IsConnected != true)
                await realtime.ConnectAsync();

            // Realtime enforces RLS on postgres_changes using this JWT.
            var accessToken = supabaseClient.Auth.CurrentSession?.AccessToken;
            if (!string.IsNullOrEmpty(accessToken))
                realtime.SetAuth(accessToken);

            IRealtimeChannel.PostgresChangesHandler handler = (_, change) =>
            {
                var mealImportDb = change.Model<MealImportDb>();
                if (mealImportDb is not null)
                    onUpdate(new Models.MealImport(mealImportDb));
            };

            // Leaving the previous job's channel (the last one) makes the client close its socket, and a join sent while
            // that happens is never answered (seen when a retry follows the job it retries). So a join that isn't
            // answered in time is repeated on a fresh channel, after making sure the socket is up.
            for (var attempt = 1; ; attempt++)
            {
                // Again for every attempt: a socket that was closed and reopened has forgotten the user's token.
                if (attempt > 1 && !string.IsNullOrEmpty(accessToken))
                    realtime.SetAuth(accessToken);

                var channel = realtime.Channel("realtime", "public", "meal_imports", "id", mealImportId.ToString());
                channel.AddPostgresChangeHandler(PostgresChangesOptions.ListenType.Updates, handler);

                // The join carries only the socket's anon key; the client sends the user's access_token after the join
                // reply, and Realtime then re-subscribes with the user's claims. Subscribe() returns on the first,
                // anonymous confirmation, while RLS still hides the row, so also wait for the user's one (MEA-43).
                var confirmations = new PostgresSubscriptionConfirmations();
                IRealtimeChannel.MessageReceivedHandler onMessage = (_, message) => confirmations.OnMessage(message);
                channel.AddMessageReceivedHandler(onMessage);

                try
                {
                    await channel.Subscribe().WaitAsync(JoinTimeout);

                    // Without the user's confirmation the channel can't see the row (RLS), so an update would be missed:
                    // try again. After the last attempt carry on anyway: RunTrigger still re-reads the row after the ack.
                    if (!string.IsNullOrEmpty(accessToken)
                        && await Task.WhenAny(confirmations.AsUser, Task.Delay(AsUserTimeout)) != confirmations.AsUser
                        && attempt < MaxJoinAttempts)
                        throw new TimeoutException("Realtime did not confirm the subscription as the user.");

                    return new Subscription(realtime, channel, handler);
                }
                catch (Exception) when (attempt < MaxJoinAttempts)
                {
                    channel.RemovePostgresChangeHandler(PostgresChangesOptions.ListenType.Updates, handler);
                    Result.Try(() => realtime.Remove(channel));

                    await Task.Delay(JoinRetryDelay);
                    if (realtime.Socket?.IsConnected != true)
                        await realtime.ConnectAsync();
                }
                finally
                {
                    channel.RemoveMessageReceivedHandler(onMessage);
                }
            }
        });
    }

    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan JoinRetryDelay = TimeSpan.FromMilliseconds(500);
    private const int MaxJoinAttempts = 3;

    private static readonly TimeSpan AsUserTimeout = TimeSpan.FromSeconds(4);

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

/// <summary>
/// Counts Realtime's "Subscribed to PostgreSQL" confirmations on a channel. The first answers the anonymous join;
/// the second comes once Realtime has re-subscribed with the access_token sent after it.
/// </summary>
internal sealed class PostgresSubscriptionConfirmations
{
    private readonly TaskCompletionSource asUser = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int confirmed;

    /// <summary>Completes on the second confirmation.</summary>
    public Task AsUser => asUser.Task;

    public void OnMessage(SocketResponse message)
    {
        if (message.Event != Supabase.Realtime.Constants.EventType.System) return;
        if (message.Payload is not { Extension: "postgres_changes", Status: "ok" }) return;

        if (Interlocked.Increment(ref confirmed) >= 2)
            asUser.TrySetResult();
    }
}
