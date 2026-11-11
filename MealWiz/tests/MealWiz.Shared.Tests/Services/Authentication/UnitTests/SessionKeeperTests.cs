using System.Net;
using System.Text;
using MealWiz.Shared.Services.Authentication;
using Microsoft.Extensions.Time.Testing;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Xunit;
using Client = Supabase.Client;

namespace MealWiz.Shared.Tests.Services.Authentication.UnitTests;

/// <summary>
/// A real Supabase client pointed at a local HttpListener that plays the token endpoint, with a fake clock
/// driving the keeper's timers.
/// </summary>
public sealed class SessionKeeperTests : IDisposable
{
    private readonly TokenEndpoint _endpoint = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly FakePersistence _persistence = new();

    public void Dispose() => _endpoint.Dispose();

    [Fact]
    public async Task Expired_access_token_is_refreshed_before_start_completes()
    {
        Store(createdAgo: TimeSpan.FromHours(2));
        var keeper = Keeper(out var client);

        await keeper.StartAsync();

        Assert.False(keeper.IsSignedOut);
        Assert.Equal("new-access", client.Auth.CurrentSession?.AccessToken);
        Assert.Equal("new-access", _persistence.Saved?.AccessToken);
        Assert.Equal(1, _endpoint.Requests);
    }

    [Fact]
    public async Task Valid_session_is_not_refreshed_at_start()
    {
        Store(createdAgo: TimeSpan.FromMinutes(5));
        var keeper = Keeper(out _);

        await keeper.StartAsync();

        Assert.False(keeper.IsSignedOut);
        Assert.Equal(0, _endpoint.Requests);
    }

    [Fact]
    public async Task Nothing_stored_means_signed_out_without_a_rejection()
    {
        var keeper = Keeper(out _);
        var rejected = false;
        keeper.SessionRejected += () => rejected = true;

        await keeper.StartAsync();

        Assert.True(keeper.IsSignedOut);
        Assert.False(rejected);
    }

    [Fact]
    public async Task Start_is_idempotent()
    {
        Store(createdAgo: TimeSpan.FromHours(2));
        var keeper = Keeper(out _);

        await Task.WhenAll(keeper.StartAsync(), keeper.StartAsync());
        await keeper.StartAsync();

        Assert.Equal(1, _endpoint.Requests);
    }

    [Fact]
    public async Task Rejected_refresh_destroys_the_session_and_raises_SessionRejected()
    {
        Store(createdAgo: TimeSpan.FromHours(2));
        _endpoint.Respond(400);
        var keeper = Keeper(out _);
        var rejected = 0;
        keeper.SessionRejected += () => rejected++;

        await keeper.StartAsync();

        Assert.True(keeper.IsSignedOut);
        Assert.True(_persistence.Destroyed);
        Assert.Equal(1, rejected);
    }

    [Fact]
    public async Task Failing_refresh_keeps_the_session_and_retries_with_growing_delays_up_to_5_minutes()
    {
        Store(createdAgo: TimeSpan.FromHours(2));
        _endpoint.Respond(503);
        var keeper = Keeper(out var client);

        await keeper.StartAsync();
        Assert.False(keeper.IsSignedOut);
        Assert.NotNull(client.Auth.CurrentSession?.RefreshToken);
        Assert.Equal(1, _endpoint.Requests);

        var requests = 1;
        foreach (var delay in new[] { 15, 30, 60, 120, 240, 300, 300 })
        {
            await AdvanceAsync(TimeSpan.FromSeconds(delay - 1));
            Assert.Equal(requests, _endpoint.Requests);
            await AdvanceAsync(TimeSpan.FromSeconds(1));
            Assert.Equal(++requests, _endpoint.Requests);
        }
        Assert.False(keeper.IsSignedOut);
    }

    [Fact]
    public async Task A_success_resets_the_back_off_and_schedules_at_80_percent_of_the_lifetime()
    {
        Store(createdAgo: TimeSpan.FromHours(2));
        _endpoint.Respond(503);
        var keeper = Keeper(out _);
        await keeper.StartAsync();
        await AdvanceAsync(TimeSpan.FromSeconds(15)); // second failure, next retry in 30 s
        _endpoint.Respond(200);
        await AdvanceAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(3, _endpoint.Requests);

        // The SDK stamps the new session with the real clock, so it is due 2880 s after the test started (80 % of 3600 s).
        _endpoint.Respond(503);
        await AdvanceAsync(TimeSpan.FromSeconds(2830)); // fake time is at 2875 s
        Assert.Equal(3, _endpoint.Requests);
        await AdvanceAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(4, _endpoint.Requests);

        // The success reset the back-off: this failure retries after 15 s, not 60 s.
        await AdvanceAsync(TimeSpan.FromSeconds(14));
        Assert.Equal(4, _endpoint.Requests);
        await AdvanceAsync(TimeSpan.FromSeconds(1));
        Assert.Equal(5, _endpoint.Requests);
    }

    [Fact]
    public async Task SignOutLocally_destroys_the_session_stops_refreshing_and_raises_Changed()
    {
        Store(createdAgo: TimeSpan.FromMinutes(5));
        var keeper = Keeper(out _);
        await keeper.StartAsync();
        var changed = 0;
        keeper.Changed += () => changed++;

        await keeper.SignOutLocallyAsync();
        await AdvanceAsync(TimeSpan.FromHours(2));

        Assert.True(keeper.IsSignedOut);
        Assert.True(_persistence.Destroyed);
        Assert.Equal(1, changed);
        Assert.Equal(0, _endpoint.Requests);
    }

    // The timer callback runs on the thread pool; give its HTTP call time to land.
    private async Task AdvanceAsync(TimeSpan by)
    {
        var before = _endpoint.Requests;
        _time.Advance(by);
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(20);
            if (_endpoint.Requests != before && _endpoint.Idle)
                break;
        }
        await Task.Delay(100);
    }

    private SessionKeeper Keeper(out Client client)
    {
        client = new Client(_endpoint.Url, "test-anon-key", new SupabaseOptions
        {
            AutoRefreshToken = false,
            AutoConnectRealtime = false,
            SessionHandler = _persistence
        });
        return new SessionKeeper(client, _persistence, _time);
    }

    private void Store(TimeSpan createdAgo) => _persistence.Saved = new Session
    {
        AccessToken = "old-access",
        RefreshToken = "old-refresh",
        TokenType = "bearer",
        CreatedAt = _time.GetUtcNow().UtcDateTime - createdAgo,
        ExpiresIn = 3600,
        User = new User { Id = "u1", Email = "ada@example.com" }
    };

    private sealed class FakePersistence : IGotrueSessionPersistence<Session>
    {
        public Session? Saved { get; set; }
        public bool Destroyed { get; private set; }
        public void SaveSession(Session session) => Saved = session;
        public void DestroySession() { Saved = null; Destroyed = true; }
        public Session? LoadSession() => Saved;
    }

    private sealed class TokenEndpoint : IDisposable
    {
        private readonly HttpListener _listener = new();
        private int _status = 200;
        private int _requests;
        private int _inFlight;

        public TokenEndpoint()
        {
            var port = Random.Shared.Next(20000, 40000);
            for (; ; port++)
            {
                try
                {
                    _listener.Prefixes.Clear();
                    _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                    _listener.Start();
                    break;
                }
                catch (HttpListenerException) { }
            }
            Url = $"http://127.0.0.1:{port}";
            _ = Task.Run(Loop);
        }

        public string Url { get; }
        public int Requests => Volatile.Read(ref _requests);
        public bool Idle => Volatile.Read(ref _inFlight) == 0;
        public void Respond(int status) => _status = status;

        private async Task Loop()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }

                Interlocked.Increment(ref _inFlight);
                if (ctx.Request.Url!.AbsolutePath.EndsWith("/token")) Interlocked.Increment(ref _requests);
                var status = _status;
                ctx.Response.StatusCode = status;
                ctx.Response.ContentType = "application/json";
                var body = status == 200
                    ? """{"access_token":"new-access","token_type":"bearer","expires_in":3600,"refresh_token":"new-refresh","user":{"id":"u1","email":"ada@example.com"}}"""
                    : """{"message":"failure"}""";
                var bytes = Encoding.UTF8.GetBytes(body);
                await ctx.Response.OutputStream.WriteAsync(bytes);
                ctx.Response.Close();
                Interlocked.Decrement(ref _inFlight);
            }
        }

        public void Dispose() => _listener.Close();
    }
}
