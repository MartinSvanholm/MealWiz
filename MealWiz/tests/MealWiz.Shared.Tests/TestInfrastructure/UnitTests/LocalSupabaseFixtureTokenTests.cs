using System.Text;
using Xunit;

namespace MealWiz.Shared.Tests.TestInfrastructure.UnitTests;

/// <summary>MEA-55: a freshly signed-in user's token is held back until PostgREST will accept its <c>iat</c>.</summary>
public class LocalSupabaseFixtureTokenTests
{
    private static readonly DateTimeOffset IssuedAt = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static string Token(string payloadJson)
    {
        static string Encode(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{Encode("{\"alg\":\"HS256\"}")}.{Encode(payloadJson)}.signature";
    }

    private static string TokenIssuedAt(DateTimeOffset iat) => Token($"{{\"sub\":\"u\",\"iat\":{iat.ToUnixTimeSeconds()}}}");

    [Fact]
    public void A_token_issued_this_instant_waits_the_settle_time() =>
        Assert.Equal(LocalSupabaseFixture.TokenSettleTime, LocalSupabaseFixture.DelayUntilTokenIsAccepted(TokenIssuedAt(IssuedAt), IssuedAt));

    [Fact]
    public void A_token_issued_half_a_second_ago_waits_the_rest() =>
        Assert.Equal(
            LocalSupabaseFixture.TokenSettleTime - TimeSpan.FromMilliseconds(500),
            LocalSupabaseFixture.DelayUntilTokenIsAccepted(TokenIssuedAt(IssuedAt), IssuedAt.AddMilliseconds(500)));

    [Fact]
    public void An_older_token_does_not_wait() =>
        Assert.Equal(TimeSpan.Zero, LocalSupabaseFixture.DelayUntilTokenIsAccepted(TokenIssuedAt(IssuedAt), IssuedAt.AddSeconds(5)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.!!!.c")]
    public void An_unreadable_token_does_not_wait(string? token) =>
        Assert.Equal(TimeSpan.Zero, LocalSupabaseFixture.DelayUntilTokenIsAccepted(token, IssuedAt));

    [Fact]
    public void A_token_without_iat_does_not_wait() =>
        Assert.Equal(TimeSpan.Zero, LocalSupabaseFixture.DelayUntilTokenIsAccepted(Token("{\"sub\":\"u\"}"), IssuedAt));
}
