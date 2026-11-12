using MealWiz.Shared.Services.Authentication;
using Supabase.Gotrue.Exceptions;
using Xunit;

namespace MealWiz.Shared.Tests.Services.Authentication.UnitTests;

public class RefreshFailureTests
{
    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    public void Rejected_statuses(int status) =>
        Assert.Equal(RefreshFailureKind.Rejected, RefreshFailure.Classify(Gotrue(status)));

    [Theory]
    [InlineData(0)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(404)]
    public void Transient_statuses(int status) =>
        Assert.Equal(RefreshFailureKind.Transient, RefreshFailure.Classify(Gotrue(status)));

    [Fact]
    public void Offline_reason_is_transient_even_with_a_rejecting_status() =>
        Assert.Equal(RefreshFailureKind.Transient,
            RefreshFailure.Classify(Gotrue(400, FailureHint.Reason.Offline)));

    [Fact]
    public void Network_exceptions_and_unknown_errors_are_transient()
    {
        Assert.Equal(RefreshFailureKind.Transient, RefreshFailure.Classify(new TaskCanceledException()));
        Assert.Equal(RefreshFailureKind.Transient, RefreshFailure.Classify(new HttpRequestException()));
        Assert.Equal(RefreshFailureKind.Transient, RefreshFailure.Classify(new InvalidOperationException()));
    }

    // StatusCode has an internal setter: set its backing field.
    private static GotrueException Gotrue(int status, FailureHint.Reason reason = FailureHint.Reason.Unknown)
    {
        var exception = new GotrueException("failure", reason);
        if (status != 0)
        {
            typeof(GotrueException)
                .GetField("<StatusCode>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                .SetValue(exception, status);
        }
        return exception;
    }
}
