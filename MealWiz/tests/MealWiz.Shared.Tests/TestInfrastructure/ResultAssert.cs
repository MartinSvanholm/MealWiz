using FluentResults;
using Xunit;

namespace MealWiz.Shared.Tests.TestInfrastructure;

/// <summary>Asserts on handler results with the errors in the failure message, so a failed run says why.</summary>
public static class ResultAssert
{
    public static void Ok(ResultBase result) =>
        Assert.True(result.IsSuccess, $"Expected success, got: {Describe(result)}");

    public static T Ok<T>(Result<T> result)
    {
        Ok((ResultBase)result);
        return result.Value;
    }

    public static void Failed(ResultBase result) =>
        Assert.True(result.IsFailed, "Expected a failed result, got success.");

    /// <summary>Fails with exactly <paramref name="message"/> among the errors.</summary>
    public static void Failed(ResultBase result, string message)
    {
        Failed(result);
        Assert.Contains(result.Errors, e => e.Message == message);
    }

    private static string Describe(ResultBase result) => string.Join("; ", result.Errors.Select(e => e.Message));
}
