using MealWiz.Shared.Features.MealImport.State;
using Newtonsoft.Json;
using Supabase.Realtime.Socket;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealImport.UnitTests.State;

public class PostgresSubscriptionConfirmationsTests
{
    private readonly PostgresSubscriptionConfirmations _confirmations = new();

    [Fact]
    public void AsUser_FirstConfirmation_IsTheAnonymousJoin_StillWaits()
    {
        _confirmations.OnMessage(System("postgres_changes", "ok"));

        Assert.False(_confirmations.AsUser.IsCompleted);
    }

    [Fact]
    public void AsUser_SecondConfirmation_Completes()
    {
        _confirmations.OnMessage(System("postgres_changes", "ok"));
        _confirmations.OnMessage(System("postgres_changes", "ok"));

        Assert.True(_confirmations.AsUser.IsCompletedSuccessfully);
    }

    [Theory]
    [InlineData("postgres_changes", "error")]
    [InlineData("broadcast", "ok")]
    public void AsUser_OtherSystemMessages_DontCount(string extension, string status)
    {
        _confirmations.OnMessage(System("postgres_changes", "ok"));
        _confirmations.OnMessage(System(extension, status));

        Assert.False(_confirmations.AsUser.IsCompleted);
    }

    [Fact]
    public void AsUser_PostgresChanges_DontCount()
    {
        _confirmations.OnMessage(System("postgres_changes", "ok"));
        _confirmations.OnMessage(Message("postgres_changes", new SocketResponsePayload { Status = "ok" }));

        Assert.False(_confirmations.AsUser.IsCompleted);
    }

    private static SocketResponse System(string extension, string status) =>
        Message("system", new SocketResponsePayload { Extension = extension, Status = status, Message = "Subscribed to PostgreSQL" });

    private static SocketResponse Message(string @event, SocketResponsePayload payload) =>
        new(new JsonSerializerSettings()) { _event = @event, Payload = payload };
}
