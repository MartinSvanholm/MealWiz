using MealWiz.Shared.Features.Authentication;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Authentication.UnitTests;

public class GotrueErrorCodeTests
{
    [Theory]
    [InlineData("""{"code":422,"error_code":"weak_password","msg":"Password should be at least 6 characters."}""", "weak_password")]
    [InlineData("""{"code":422,"error_code":"same_password","msg":"New password should be different"}""", "same_password")]
    [InlineData("""{"code":422,"error_code":"email_exists","msg":"A user with this email address has already been registered"}""", "email_exists")]
    [InlineData("""{"code":422,"error_code":"validation_failed","msg":"Unable to validate email address"}""", "validation_failed")]
    public void Reads_the_error_code_of_a_gotrue_body(string body, string expected) =>
        Assert.Equal(expected, GotrueErrorCode.Read(body));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"msg":"no code"}""")]
    [InlineData("""{"error_code":5}""")]
    public void Anything_else_has_no_code(string? body) => Assert.Null(GotrueErrorCode.Read(body));
}
