using Supabase;

namespace MealWiz.Shared.Tests.TestInfrastructure.Builders;

internal static class ClientExtensions
{
    /// <summary>The signed-in user's id, which RLS requires as <c>created_by</c> on every insert.</summary>
    public static Guid CurrentUserId(this Client client) =>
        Guid.Parse(client.Auth.CurrentUser?.Id ?? throw new InvalidOperationException("The client isn't signed in."));

    /// <summary>A short random suffix, so default names are unique enough to tell rows apart when debugging.</summary>
    public static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8];
}
