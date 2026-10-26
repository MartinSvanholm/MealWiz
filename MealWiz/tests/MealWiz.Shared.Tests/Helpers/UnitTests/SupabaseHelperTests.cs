using MealWiz.Shared.Helpers;
using Supabase.Gotrue.Exceptions;
using Supabase.Postgrest.Exceptions;
using Xunit;

namespace MealWiz.Shared.Tests.Helpers.UnitTests;

public class SupabaseHelperTests
{
    [Fact]
    public void ConvertGoTrueException_WithoutResponse_IsServiceUnavailable()
    {
        var response = new GotrueException("whatever").ConvertGoTrueException();

        Assert.Equal(503, response.code);
        Assert.Equal("No response from server", response.msg);
        Assert.Null(response.error_code);
    }

    [Fact]
    public void GetSupabaseErrorMessage_JsonMessage_MapsAllFields()
    {
        var exception = new PostgrestException("""
            {"code":"23505","details":"Key (id)=(1) already exists.","hint":"Use another id","message":"duplicate key value"}
            """);

        var message = SupabaseHelper.GetSupabaseErrorMessage(exception);

        Assert.Equal("23505", message.Code);
        Assert.Equal("Key (id)=(1) already exists.", message.Details);
        Assert.Equal("Use another id", message.Hint);
        Assert.Equal("duplicate key value", message.Message);
    }

    [Theory]
    [InlineData("No such host is known.")]
    [InlineData("")]
    [InlineData("<html>Bad Gateway</html>")]
    public void GetSupabaseErrorMessage_NonJsonMessage_FallsBackToTheRawMessage(string rawMessage)
    {
        var exception = new PostgrestException(rawMessage);

        var message = SupabaseHelper.GetSupabaseErrorMessage(exception);

        Assert.Equal(rawMessage, message.Message);
    }

    [Theory]
    [InlineData("""{"code":"PGRST301"}""")]
    [InlineData("""{"code":"PGRST301","message":""}""")]
    [InlineData("null")]
    public void GetSupabaseErrorMessage_JsonWithoutAMessage_FallsBackToTheRawMessage(string rawMessage)
    {
        var message = SupabaseHelper.GetSupabaseErrorMessage(new GotrueException(rawMessage));

        Assert.Equal(rawMessage, message.Message);
    }
}
