using System.Reflection;
using FluentResults;
using MealWiz.Functions.Functions.CleanUpMealPictures;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.CleanUpMealPictures.UnitTests;

public class CleanUpMealPicturesFunctionTests
{
    [Fact]
    public async Task Run_CleansUpPicturesOlderThan24Hours()
    {
        var handler = new Mock<ICleanUpMealPicturesHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Ok(3));
        var before = DateTimeOffset.UtcNow;

        await new CleanUpMealPicturesFunction(handler.Object, NullLogger<CleanUpMealPicturesFunction>.Instance)
            .Run(new TimerInfo(), TestContext.Current.CancellationToken);

        handler.Verify(h => h.HandleAsync(
            It.Is<DateTimeOffset>(c => c >= before - TimeSpan.FromHours(24) && c <= DateTimeOffset.UtcNow - TimeSpan.FromHours(24)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_AFailedCleanUp_DoesNotThrow()
    {
        var handler = new Mock<ICleanUpMealPicturesHandler>();
        handler.Setup(h => h.HandleAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Fail<int>("down"));

        await new CleanUpMealPicturesFunction(handler.Object, NullLogger<CleanUpMealPicturesFunction>.Instance)
            .Run(new TimerInfo(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public void TimerTrigger_RunsDailyAt0317Utc()
    {
        var trigger = typeof(CleanUpMealPicturesFunction)
            .GetMethod(nameof(CleanUpMealPicturesFunction.Run))!
            .GetParameters()
            .Select(p => p.GetCustomAttribute<TimerTriggerAttribute>())
            .Single(a => a is not null)!;

        Assert.Equal("0 17 3 * * *", trigger.Schedule);
    }
}
