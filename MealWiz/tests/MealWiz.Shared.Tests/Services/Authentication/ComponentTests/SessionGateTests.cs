using Bunit;
using MealWiz.Shared.Services.Authentication;
using Bunit.TestDoubles;
using Microsoft.Extensions.Time.Testing;
using MudBlazor.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Supabase;
using Supabase.Gotrue;
using Supabase.Gotrue.Interfaces;
using Xunit;
using Client = Supabase.Client;

namespace MealWiz.Shared.Tests.Services.Authentication.ComponentTests;

public class SessionGateTests : BunitContext
{
    private readonly GatedKeeper _keeper;
    private FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);
    private static string TestId(string id) => $"[data-testid=\"{id}\"]";

    public SessionGateTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddSingleton<TimeProvider>(Time);
        _keeper = new GatedKeeper(Time);
        Services.AddSingleton<SessionKeeper>(_keeper);
    }

    private IRenderedComponent<SessionGate> RenderGate() =>
        Render<SessionGate>(p => p.Add(g => g.ChildContent, "<p data-testid=\"app\">app</p>"));

    [Fact]
    public void Shows_a_spinner_and_not_the_app_until_the_session_is_started()
    {
        var cut = RenderGate();

        Assert.NotEmpty(cut.FindAll(TestId("session-gate-loading")));
        Assert.Empty(cut.FindAll(TestId("app")));
    }

    [Fact]
    public void Renders_the_app_once_the_session_is_started()
    {
        var cut = RenderGate();

        _keeper.Complete();

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(TestId("app"))));
        Assert.Empty(cut.FindAll(TestId("session-gate-loading")));
    }

    [Fact]
    public void Renders_the_app_anyway_after_10_seconds()
    {
        var cut = RenderGate();

        Time.Advance(TimeSpan.FromSeconds(9));
        Assert.Empty(cut.FindAll(TestId("app")));
        Time.Advance(TimeSpan.FromSeconds(2));

        cut.WaitForAssertion(() => Assert.NotEmpty(cut.FindAll(TestId("app"))));
    }

    [Fact]
    public void A_rejected_session_reloads_the_page_on_the_login_page()
    {
        var cut = RenderGate();

        _keeper.Reject();

        var navigation = Services.GetRequiredService<NavigationManager>() as BunitNavigationManager;
        Assert.NotNull(navigation);
        cut.WaitForAssertion(() =>
        {
            var last = navigation.History.First();
            Assert.EndsWith("/login", last.Uri);
            Assert.True(last.Options.ForceLoad);
        });
    }

    private sealed class GatedKeeper(TimeProvider time)
        : SessionKeeper(new Client("http://localhost", "test-key"), new NoPersistence(), time)
    {
        private readonly TaskCompletionSource _started = new();
        public override Task StartAsync() => _started.Task;
        public void Complete() => _started.SetResult();
        public void Reject() => RaiseRejected();
    }

    private sealed class NoPersistence : IGotrueSessionPersistence<Session>
    {
        public void SaveSession(Session session) { }
        public void DestroySession() { }
        public Session? LoadSession() => null;
    }
}
