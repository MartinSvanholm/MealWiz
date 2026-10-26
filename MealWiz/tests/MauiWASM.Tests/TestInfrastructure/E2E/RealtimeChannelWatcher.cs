using System.Text.Json;
using Microsoft.Playwright;

namespace MauiWASM.Tests.TestInfrastructure.E2E;

/// <summary>
/// Reads the page's Supabase Realtime frames, so a test can make sure a channel actually delivers changes to the
/// signed-in user before it writes the row the app is waiting for.
/// <para>
/// Why it's needed (MEA-43): the app joins the channel with the anon key and only then sends the user's access_token.
/// Until Realtime has re-subscribed with the user's claims, RLS hides the row and updates are dropped. How many
/// "Subscribed to PostgreSQL" messages that takes differs between Realtime versions, so instead of counting them
/// this waits for proof of delivery: a harmless change to the row arriving over the socket.
/// </para>
/// </summary>
public sealed class RealtimeChannelWatcher
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _changesByTopic = [];
    private readonly List<(string TopicFragment, int Beyond, TaskCompletionSource Arrived)> _waiters = [];

    /// <summary>Attach before the page opens its Realtime socket, i.e. before navigating.</summary>
    public RealtimeChannelWatcher(IPage page)
    {
        page.WebSocket += (_, socket) =>
        {
            if (!socket.Url.Contains("/realtime/", StringComparison.Ordinal)) return;
            socket.FrameReceived += (_, frame) => OnFrameReceived(frame.Text);
        };
    }

    /// <summary>
    /// Calls <paramref name="touch"/> (a change the app ignores) until that change arrives on a channel whose topic
    /// contains <paramref name="topicFragment"/>. Each touch gets a second to arrive; gives up after
    /// <paramref name="timeout"/>.
    /// </summary>
    public async Task WaitUntilDeliveringAsync(string topicFragment, Func<Task> touch, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        while (true)
        {
            var seen = ChangesSeen(topicFragment);
            await touch();

            try
            {
                await WaitForChangeAsync(topicFragment, seen).WaitAsync(TimeSpan.FromSeconds(1), deadline.Token);
                return;
            }
            catch (TimeoutException)
            {
                // Not subscribed as the user yet: touch again.
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"No Realtime change arrived for '{topicFragment}' within {timeout}.");
            }
        }
    }

    private int ChangesSeen(string topicFragment)
    {
        lock (_gate)
            return _changesByTopic.Where(t => t.Key.Contains(topicFragment, StringComparison.Ordinal)).Sum(t => t.Value);
    }

    private Task WaitForChangeAsync(string topicFragment, int beyond)
    {
        var arrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _waiters.Add((topicFragment, beyond, arrived));
            Release();
        }

        return arrived.Task;
    }

    private void OnFrameReceived(string? text)
    {
        if (text is null) return;

        string? topic;
        try
        {
            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            if (!root.TryGetProperty("event", out var e) || e.GetString() != "postgres_changes") return;
            topic = root.TryGetProperty("topic", out var t) ? t.GetString() : null;
        }
        catch (JsonException)
        {
            return;
        }

        if (topic is null) return;

        lock (_gate)
        {
            _changesByTopic[topic] = _changesByTopic.GetValueOrDefault(topic) + 1;
            Release();
        }
    }

    // Called under _gate.
    private void Release()
    {
        foreach (var waiter in _waiters.ToList())
        {
            var seen = _changesByTopic.Where(t => t.Key.Contains(waiter.TopicFragment, StringComparison.Ordinal)).Sum(t => t.Value);
            if (seen <= waiter.Beyond) continue;

            waiter.Arrived.TrySetResult();
            _waiters.Remove(waiter);
        }
    }
}
