using System.Net;
using MealWiz.Shared.Features.Account;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Services.Authentication;
using MealWiz.Shared.Tests.TestInfrastructure;
using Microsoft.Extensions.Time.Testing;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Xunit;
using Delete = MealWiz.Shared.Features.Account.DeleteAccount.DeleteAccount;
using Client = Supabase.Client;

namespace MealWiz.Shared.Tests.Features.Account.UnitTests;

/// <summary>
/// The command against a stubbed Function: only a 204 signs out (locally), every other answer comes back as the
/// message the panel shows, and the request carries the bearer token, the function key and the password.
/// </summary>
public class DeleteAccountTests
{
    [Fact]
    public async Task NoContent_SignsOutLocally_AndSucceeds()
    {
        var run = await RunAsync(new Stub(HttpStatusCode.NoContent));

        Assert.True(run.Result.IsSuccess);
        Assert.True(run.Keeper.IsSignedOut);
        Assert.True(run.Persistence.Destroyed);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "That password isn't right.")]
    [InlineData(HttpStatusCode.Unauthorized, "Your session has expired. Log in again.")]
    [InlineData(HttpStatusCode.InternalServerError, "Couldn't delete your account. Try again later.")]
    [InlineData(HttpStatusCode.BadGateway, "Couldn't delete your account. Try again later.")]
    public async Task AnyOtherAnswer_FailsWithItsMessage_AndStaysSignedIn(HttpStatusCode status, string message)
    {
        var run = await RunAsync(new Stub(status));

        Assert.True(run.Result.IsFailed);
        Assert.Equal(message, run.Result.Errors[0].Message);
        Assert.False(run.Keeper.IsSignedOut);
        Assert.False(run.Persistence.Destroyed);
    }

    [Fact]
    public async Task NetworkError_FailsWithTheGenericMessage_AndStaysSignedIn()
    {
        var run = await RunAsync(new Stub(HttpStatusCode.OK, throws: true));

        Assert.Equal("Couldn't delete your account. Try again later.", run.Result.Errors[0].Message);
        Assert.False(run.Keeper.IsSignedOut);
    }

    [Fact]
    public async Task NoFunctionUrl_FailsWithoutSending()
    {
        var stub = new Stub(HttpStatusCode.NoContent);
        var run = await RunAsync(stub, new MealImportFunctionOptions("", null));

        Assert.True(run.Result.IsFailed);
        Assert.Null(stub.Request);
        Assert.False(run.Keeper.IsSignedOut);
    }

    [Fact]
    public async Task SendsTheBearerTokenFunctionKeyAndPassword()
    {
        var stub = new Stub(HttpStatusCode.NoContent);
        await RunAsync(stub);

        Assert.Equal("https://functions.example.com/api/DeleteAccount", stub.Request!.RequestUri!.ToString());
        Assert.Equal("Bearer access-token", stub.Request.Headers.Authorization!.ToString());
        Assert.Equal("fn-key", Assert.Single(stub.Request.Headers.GetValues("x-functions-key")));
        Assert.Equal("""{"password":"pw"}""", stub.Body);
    }

    private static async Task<Run> RunAsync(Stub stub, MealImportFunctionOptions? options = null)
    {
        var persistence = new FakePersistence
        {
            Saved = new Session
            {
                AccessToken = "access-token", RefreshToken = "refresh", TokenType = "bearer",
                CreatedAt = DateTime.UtcNow, ExpiresIn = 3600, User = new User { Id = "u1", Email = "ada@example.com" }
            }
        };
        var client = new Client("http://127.0.0.1:1", "anon", new SupabaseOptions
        {
            AutoRefreshToken = false, AutoConnectRealtime = false, SessionHandler = persistence
        });
        client.Auth.LoadSession();
        var keeper = new SessionKeeper(client, persistence, new FakeTimeProvider(DateTimeOffset.UtcNow));

        using var http = new HttpClient(stub);
        var handler = new Delete.Handler(
            http, options ?? new MealImportFunctionOptions("https://functions.example.com", "fn-key"),
            client, keeper, TestLocalizer.For<AccountResource>());
        var result = await handler.Handle(new Delete.Command("pw"), TestContext.Current.CancellationToken);
        return new Run(result, keeper, persistence);
    }

    private sealed record Run(FluentResults.Result Result, SessionKeeper Keeper, FakePersistence Persistence);

    private sealed class FakePersistence : IGotrueSessionPersistence<Session>
    {
        public Session? Saved { get; set; }
        public bool Destroyed { get; private set; }
        public void SaveSession(Session session) => Saved = session;
        public void DestroySession() { Saved = null; Destroyed = true; }
        public Session? LoadSession() => Saved;
    }

    private sealed class Stub(HttpStatusCode status, bool throws = false) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (throws) throw new HttpRequestException("down");
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }
}
