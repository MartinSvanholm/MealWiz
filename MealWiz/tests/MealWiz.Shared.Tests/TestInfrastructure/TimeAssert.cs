using Xunit;

namespace MealWiz.Shared.Tests.TestInfrastructure;

public static class TimeAssert
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(1);

    /// <summary>
    /// <paramref name="actual"/> is a timestamp written at or after <paramref name="utcBefore"/>, give or take clock
    /// skew between the test and the Docker stack. The Supabase client reads timestamptz columns back as local
    /// wall-clock time with <see cref="DateTimeKind.Unspecified"/>, so anything not marked UTC is treated as local.
    /// </summary>
    public static void CloseTo(DateTime utcBefore, DateTime? actual)
    {
        Assert.NotNull(actual);
        var actualUtc = actual.Value.Kind == DateTimeKind.Utc
            ? actual.Value
            : DateTime.SpecifyKind(actual.Value, DateTimeKind.Local).ToUniversalTime();

        Assert.InRange(actualUtc, utcBefore - Tolerance, DateTime.UtcNow + Tolerance);
    }
}
