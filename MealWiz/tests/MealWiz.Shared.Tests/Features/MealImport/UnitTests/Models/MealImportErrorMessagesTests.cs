using MealWiz.Shared.Features.MealImport;
using MealWiz.Shared.Features.MealImport.Models;
using MealWiz.Shared.Tests.TestInfrastructure;
using Xunit;

namespace MealWiz.Shared.Tests.Features.MealImport.UnitTests.Models;

/// <summary>A failed import shows the localized message for its error code, never the stored English error_message.</summary>
public class MealImportErrorMessagesTests
{
    private readonly Microsoft.Extensions.Localization.IStringLocalizer<MealImportResource> _localizer =
        TestLocalizer.For<MealImportResource>();

    [Theory]
    [InlineData(MealImportErrorCode.InvalidUrl, "That link isn't valid. Use a link that starts with http:// or https://.")]
    [InlineData(MealImportErrorCode.NoRecipeFound, "We couldn't find a recipe on that page.")]
    [InlineData(MealImportErrorCode.FetchFailed, "We couldn't fetch that page. Check the link and try again.")]
    [InlineData(MealImportErrorCode.Unreachable, "We couldn't reach the meal import service. Please try again.")]
    [InlineData(MealImportErrorCode.TimedOut, "The meal import took too long. Please try again.")]
    [InlineData(MealImportErrorCode.Unknown, "The meal import failed. Please try again.")]
    [InlineData(MealImportErrorCode.NoMealFound, "We couldn't find a meal or a recipe in the picture.")]
    [InlineData(MealImportErrorCode.DailyLimitReached, "You've reached today's limit for picture imports. Try again tomorrow.")]
    [InlineData(MealImportErrorCode.PictureUnreadable, "We couldn't read the picture. Try a different picture.")]
    [InlineData(MealImportErrorCode.AiUnavailable, "The AI service is unavailable right now. Please try again later.")]
    public void ToMessage_EachCode_HasItsOwnMessage(MealImportErrorCode code, string expected)
    {
        Assert.Equal(expected, _localizer.ToMessage(code));
    }

    [Theory]
    [InlineData(MealImportErrorCode.NoMealFound, "Vi kunne ikke finde et måltid eller en opskrift på billedet.")]
    [InlineData(MealImportErrorCode.DailyLimitReached, "Du har nået dagens grænse for import fra billeder. Prøv igen i morgen.")]
    [InlineData(MealImportErrorCode.PictureUnreadable, "Vi kunne ikke læse billedet. Prøv med et andet billede.")]
    [InlineData(MealImportErrorCode.AiUnavailable, "AI-tjenesten er ikke tilgængelig lige nu. Prøv igen senere.")]
    public void ToMessage_PictureCodes_InDanish_AreDanish(MealImportErrorCode code, string expected)
    {
        using var _ = CultureScope.Use("da");

        Assert.Equal(expected, _localizer.ToMessage(code));
    }

    [Fact]
    public void ToMessage_NoCode_IsTheGenericImportFailure()
    {
        Assert.Equal(_localizer.ToMessage(MealImportErrorCode.Unknown), _localizer.ToMessage(null));
    }

    [Fact]
    public void ToMessage_EveryCode_HasAMessage()
    {
        foreach (var code in Enum.GetValues<MealImportErrorCode>())
            Assert.False(_localizer[$"ImportError_{code}"].ResourceNotFound, $"ImportError_{code} is missing");
    }

    [Fact]
    public void ToMessage_InDanish_IsDanish()
    {
        using var _ = CultureScope.Use("da");

        Assert.Equal("Vi kunne ikke finde en opskrift på den side.", _localizer.ToMessage(MealImportErrorCode.NoRecipeFound));
    }
}
