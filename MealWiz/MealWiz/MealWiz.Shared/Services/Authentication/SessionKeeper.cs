using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Client = Supabase.Client;

namespace MealWiz.Shared.Services.Authentication;

/// <summary>
/// Owns the session's lifetime instead of the SDK's refresh (gotrue-csharp 6.0.3 deletes expired sessions and treats
/// network errors as sign-outs). Loads the stored session, refreshes it when due and keeps refreshing at 80 % of the
/// token lifetime. Only a refresh the server rejects ends the session.
/// </summary>
public class SessionKeeper : IDisposable
{
    private static readonly TimeSpan DueMargin = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan FirstRetry = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaxRetry = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromDays(1);

    private readonly Client _client;
    private readonly IGotrueSessionPersistence<Session> _persistence;
    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private Task? _start;
    private ITimer? _timer;
    private TimeSpan _backoff = FirstRetry;
    private bool _disposed;
    private bool _signingOutLocally;

    public SessionKeeper(Client client, IGotrueSessionPersistence<Session> persistence, TimeProvider time)
    {
        _client = client;
        _persistence = persistence;
        _time = time;
        _client.Auth.AddStateChangedListener(OnAuthStateChanged);
    }

    // A sign-in after startup (login, password reset) starts the keeping; a sign-out through the SDK ends it.
    // The auth state provider listens to Changed only, so it never reads this state before it is updated.
    private void OnAuthStateChanged(IGotrueClient<User, Session> sender, Constants.AuthState state)
    {
        if (_signingOutLocally) return;

        if (_start is not null) // before that, StartAsync decides on its own once it has loaded the stored session
        {
            switch (state)
            {
                case Constants.AuthState.SignedIn:
                    IsSignedOut = false;
                    _backoff = FirstRetry;
                    ScheduleNext();
                    break;
                case Constants.AuthState.SignedOut:
                    IsSignedOut = true;
                    StopTimer();
                    break;
            }
        }

        Changed?.Invoke();
    }

    public bool IsSignedOut { get; private set; }

    public event Action? Changed;

    public event Action? SessionRejected;

    protected void RaiseRejected() => SessionRejected?.Invoke();

    public virtual Task StartAsync()
    {
        lock (_lock)
        {
            return _start ??= StartCoreAsync();
        }
    }

    /// <summary>
    /// Refreshes the session now (e.g. after the email changed, so the new access token carries the new address),
    /// with the same failure rules as the timer: a transient failure keeps the session and backs off, a rejection ends it.
    /// </summary>
    public virtual Task RefreshNowAsync() => RefreshAsync();

    public Task SignOutLocallyAsync()
    {
        StopTimer();
        _persistence.DestroySession();
        IsSignedOut = true;

        // Nothing is stored any more, so loading drops the client's in-memory session too: handlers stop seeing a
        // signed-in user at once, not only after the app reloads. The SDK announces that as a sign-out; it is this
        // one, so Changed is raised once, below.
        _signingOutLocally = true;
        try { _client.Auth.LoadSession(); }
        finally { _signingOutLocally = false; }

        Changed?.Invoke();
        return Task.CompletedTask;
    }

    private async Task StartCoreAsync()
    {
        _client.Auth.LoadSession();

        var session = _client.Auth.CurrentSession;
        if (string.IsNullOrEmpty(session?.RefreshToken))
        {
            IsSignedOut = true;
            return;
        }

        IsSignedOut = false;

        if (IsDue(session))
            await RefreshAsync();
        else
            ScheduleNext();

        // Meal import needs realtime; it's connected here because AutoConnectRealtime is off.
        _ = ConnectRealtimeAsync();
    }

    private async Task ConnectRealtimeAsync()
    {
        try { await _client.Realtime.ConnectAsync(); }
        catch { /* realtime reconnects on its own; a failure here must not affect the session */ }
    }

    private bool IsDue(Session session) =>
        session.CreatedAt.ToUniversalTime().AddSeconds(session.ExpiresIn) - _time.GetUtcNow().UtcDateTime <= DueMargin;

    private async Task RefreshAsync()
    {
        var session = _client.Auth.CurrentSession;
        if (IsSignedOut || string.IsNullOrEmpty(session?.RefreshToken))
            return;

        try
        {
            var gotrue = (Supabase.Gotrue.Client)_client.Auth;
            await gotrue.RefreshToken(session.AccessToken!, session.RefreshToken);
            _backoff = FirstRetry;
            ScheduleNext();
            Changed?.Invoke();
        }
        catch (Exception ex) when (RefreshFailure.Classify(ex) == RefreshFailureKind.Rejected)
        {
            await SignOutLocallyAsync();
            RaiseRejected();
        }
        catch
        {
            // Transient: stay signed in, retry with a growing delay.
            Schedule(_backoff);
            _backoff = TimeSpan.FromTicks(Math.Min(_backoff.Ticks * 2, MaxRetry.Ticks));
        }
    }

    private void ScheduleNext()
    {
        var session = _client.Auth.CurrentSession;
        if (session is null)
            return;

        var refreshAt = session.CreatedAt.ToUniversalTime().AddSeconds(session.ExpiresIn * 0.8);
        var delay = refreshAt - _time.GetUtcNow().UtcDateTime;
        Schedule(delay < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : delay);
    }

    private void Schedule(TimeSpan delay)
    {
        lock (_lock)
        {
            if (_disposed || IsSignedOut)
                return;

            _timer?.Dispose();
            // Capped: a timer can not wait longer than ~49 days, and SetSession (sign-up, password reset) stores an
            // absolute expiry as the lifetime, which would otherwise ask for decades.
            _timer = _time.CreateTimer(_ => _ = RefreshAsync(), null, delay > MaxDelay ? MaxDelay : delay, Timeout.InfiniteTimeSpan);
        }
    }

    private void StopTimer()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        StopTimer();
    }
}
