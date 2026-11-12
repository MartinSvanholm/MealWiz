using Supabase.Gotrue.Exceptions;

namespace MealWiz.Shared.Services.Authentication;

public enum RefreshFailureKind
{
    /// <summary>Offline, timeout, 5xx, 429 or anything unknown: keep the session and retry.</summary>
    Transient,

    /// <summary>The server rejected the refresh token (400, 401, 403): the session is over.</summary>
    Rejected
}

public static class RefreshFailure
{
    public static RefreshFailureKind Classify(Exception exception)
    {
        if (exception is GotrueException gotrue)
        {
            if (gotrue.Reason == FailureHint.Reason.Offline)
                return RefreshFailureKind.Transient;

            return gotrue.StatusCode is 400 or 401 or 403
                ? RefreshFailureKind.Rejected
                : RefreshFailureKind.Transient;
        }

        // TaskCanceledException, HttpRequestException and everything else we don't know: never sign out on it.
        return RefreshFailureKind.Transient;
    }
}
