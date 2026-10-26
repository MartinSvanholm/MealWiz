using System.Text.Json;
using Microsoft.Playwright;

namespace MauiWASM.Tests.TestInfrastructure.E2E;

/// <summary>
/// Reads the page's Supabase Realtime frames to tell when a channel's postgres_changes subscription is really live
/// for the signed-in user, so a test can write a row without racing the subscription.
/// <para>
/// Why it's needed (MEA-43): the app joins the channel with the anon key and only then sends the user's access_token,
/// which makes Realtime re-subscribe. Until that second "Subscribed to PostgreSQL" arrives, RLS hides the user's row
/// and updates are dropped. The channel counts as live once the access_token has been sent and every join and token
/// frame has been confirmed.
/// </para>
/// </summary>
public sealed class RealtimeChannelWatcher
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Channel> _channels = [];
    private readonly List<(string TopicFragment, TaskCompletionSource Live)> _waiters = [];

    /// <summary>Attach before the page opens its Realtime socket, i.e. before navigating.</summary>
    public RealtimeChannelWatcher(IPage page)
    {
        page.WebSocket += (_, socket) =>
        {
            if (!socket.Url.Contains("/realtime/", StringComparison.Ordinal)) return;
            socket.FrameSent += (_, frame) => OnFrame(frame.Text, sent: true);
            socket.FrameReceived += (_, frame) => OnFrame(frame.Text, sent: false);
        };
    }

    /// <summary>Completes once a channel whose topic contains <paramref name="topicFragment"/> is live.</summary>
    public Task WaitUntilLiveAsync(string topicFragment)
    {
        var live = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_gate)
        {
            _waiters.Add((topicFragment, live));
            Release();
        }

        return live.Task;
    }

    private void OnFrame(string? text, bool sent)
    {
        if (text is null) return;

        string? topic, @event;
        bool subscribed;
        try
        {
            using var json = JsonDocument.Parse(text);
            var root = json.RootElement;
            topic = root.TryGetProperty("topic", out var t) ? t.GetString() : null;
            @event = root.TryGetProperty("event", out var e) ? e.GetString() : null;
            subscribed = !sent && @event == "system"
                && root.TryGetProperty("payload", out var payload)
                && payload.TryGetProperty("extension", out var extension) && extension.GetString() == "postgres_changes"
                && payload.TryGetProperty("status", out var status) && status.GetString() == "ok";
        }
        catch (JsonException)
        {
            return;
        }

        if (topic is null) return;

        lock (_gate)
        {
            if (!_channels.TryGetValue(topic, out var channel))
                _channels[topic] = channel = new Channel();

            if (sent && @event == "phx_join") channel.Requested++;
            if (sent && @event == "access_token")
            {
                channel.Requested++;
                channel.HasUserToken = true;
            }
            if (subscribed) channel.Confirmed++;

            Release();
        }
    }

    // Called under _gate.
    private void Release()
    {
        foreach (var waiter in _waiters.ToList())
        {
            var live = _channels.Any(c => c.Key.Contains(waiter.TopicFragment, StringComparison.Ordinal)
                                          && c.Value.HasUserToken && c.Value.Confirmed >= c.Value.Requested);
            if (!live) continue;

            waiter.Live.TrySetResult();
            _waiters.Remove(waiter);
        }
    }

    private sealed class Channel
    {
        public int Requested { get; set; }
        public int Confirmed { get; set; }
        public bool HasUserToken { get; set; }
    }
}
