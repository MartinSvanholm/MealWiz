using Supabase.Realtime;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealImport.UnitTests.State;

public class MealImportRealtimeSubscriberTests
{
    /// <summary>
    /// Supabase 1.1.1 depends on Supabase.Realtime 7.0.2, whose socket calls ClientWebSocketOptions.SetRequestHeader
    /// even in the browser. That throws PlatformNotSupportedException in Blazor WASM, so the subscriber could never
    /// connect and backgrounded imports only resolved via the post-ack re-read. MealWiz.Shared pins 7.1.0+, which
    /// skips the headers in the browser. The browser path can't run here, so this guards the pin instead.
    /// </summary>
    [Fact]
    public void ResolvedRealtimeVersion_SupportsBlazorWasm()
    {
        var version = typeof(RealtimeSocket).Assembly.GetName().Version;

        Assert.NotNull(version);
        Assert.True(version >= new Version(7, 1, 0),
            $"Supabase.Realtime {version} can't open a websocket in Blazor WASM; keep the 7.1.0+ pin in MealWiz.Shared.csproj.");
    }
}
