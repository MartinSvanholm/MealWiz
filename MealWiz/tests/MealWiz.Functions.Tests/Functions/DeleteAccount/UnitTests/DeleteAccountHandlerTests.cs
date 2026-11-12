using System.Net;
using System.Text;
using MealWiz.Functions.Functions.DeleteAccount;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.DeleteAccount.UnitTests;

/// <summary>The three GoTrue calls, against a stubbed message handler.</summary>
public class DeleteAccountHandlerTests
{
    private const string UserJson = """{"id":"11111111-1111-1111-1111-111111111111","email":"a@b.test"}""";

    [Fact]
    public async Task RightToken_RightPassword_DeletesTheUser()
    {
        var gotrue = new StubGoTrue();

        var outcome = await RunAsync(gotrue);

        Assert.Equal(DeleteAccountOutcome.Deleted, outcome);
        Assert.Equal(
            ["GET /auth/v1/user", "POST /auth/v1/token?grant_type=password",
             "DELETE /auth/v1/admin/users/11111111-1111-1111-1111-111111111111"],
            gotrue.Calls);
        Assert.Equal("Bearer user-token", gotrue.Authorization["GET /auth/v1/user"]);
        Assert.Contains("\"password\":\"pw\"", gotrue.Bodies["POST /auth/v1/token?grant_type=password"]);
        Assert.Contains("a@b.test", gotrue.Bodies["POST /auth/v1/token?grant_type=password"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GoTrueRejectsTheToken_IsUnauthorized_AndNothingElseRuns(HttpStatusCode status)
    {
        var gotrue = new StubGoTrue { UserStatus = status };

        Assert.Equal(DeleteAccountOutcome.Unauthorized, await RunAsync(gotrue));
        Assert.Equal(["GET /auth/v1/user"], gotrue.Calls);
    }

    [Fact]
    public async Task WrongPassword_IsWrongPassword_AndNeverDeletes()
    {
        var gotrue = new StubGoTrue { PasswordStatus = HttpStatusCode.BadRequest };

        Assert.Equal(DeleteAccountOutcome.WrongPassword, await RunAsync(gotrue));
        Assert.DoesNotContain(gotrue.Calls, c => c.StartsWith("DELETE"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, HttpStatusCode.OK, HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.InternalServerError, HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.InternalServerError)]
    public async Task GoTrueFailure_IsFailed_AndNeverDeletesBeforeThePasswordPassed(
        HttpStatusCode user, HttpStatusCode password, HttpStatusCode delete)
    {
        var gotrue = new StubGoTrue { UserStatus = user, PasswordStatus = password, DeleteStatus = delete };

        Assert.Equal(DeleteAccountOutcome.Failed, await RunAsync(gotrue));
        if (password != HttpStatusCode.OK || user != HttpStatusCode.OK)
            Assert.DoesNotContain(gotrue.Calls, c => c.StartsWith("DELETE"));
    }

    [Fact]
    public async Task NetworkError_IsFailed()
    {
        var gotrue = new StubGoTrue { Throw = true };

        Assert.Equal(DeleteAccountOutcome.Failed, await RunAsync(gotrue));
    }

    [Fact]
    public async Task MissingConfiguration_IsFailed()
    {
        var handler = new DeleteAccountHandler(
            new StubFactory(new StubGoTrue()), new ConfigurationBuilder().Build(), NullLogger<DeleteAccountHandler>.Instance);

        Assert.Equal(DeleteAccountOutcome.Failed,
            await handler.HandleAsync("t", "pw", TestContext.Current.CancellationToken));
    }

    private static Task<DeleteAccountOutcome> RunAsync(StubGoTrue gotrue)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Supabase:Url"] = "http://gotrue.test/",
            ["Supabase:ServiceRoleKey"] = "service-key"
        }).Build();
        var handler = new DeleteAccountHandler(new StubFactory(gotrue), config, NullLogger<DeleteAccountHandler>.Instance);
        return handler.HandleAsync("user-token", "pw", TestContext.Current.CancellationToken);
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubGoTrue : HttpMessageHandler
    {
        public HttpStatusCode UserStatus { get; init; } = HttpStatusCode.OK;
        public HttpStatusCode PasswordStatus { get; init; } = HttpStatusCode.OK;
        public HttpStatusCode DeleteStatus { get; init; } = HttpStatusCode.OK;
        public bool Throw { get; init; }
        public List<string> Calls { get; } = [];
        public Dictionary<string, string?> Authorization { get; } = [];
        public Dictionary<string, string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw) throw new HttpRequestException("down");

            var key = $"{request.Method} {request.RequestUri!.PathAndQuery}";
            Calls.Add(key);
            Authorization[key] = request.Headers.Authorization?.ToString();
            Bodies[key] = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);

            var (status, json) = request.Method.Method switch
            {
                "GET" => (UserStatus, UserJson),
                "POST" => (PasswordStatus, "{}"),
                _ => (DeleteStatus, "{}")
            };
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
