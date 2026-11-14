using MealWiz.Functions.Functions.AnalyzeMealPicture;
using Xunit;

namespace MealWiz.Functions.Tests.Functions.AnalyzeMealPicture.UnitTests;

public class MealPicturePromptTests
{
    [Theory]
    [InlineData("en", "Write the meal in English.")]
    [InlineData("da", "Write the meal in Danish.")]
    [InlineData("sv", "Write the meal in English.")]
    public void Instruction_NamesTheLanguage(string language, string expected) =>
        Assert.Equal(expected, MealPicturePrompt.Instruction(language, null));

    [Fact]
    public void Instruction_AddsATrimmedHint() =>
        Assert.Equal("Write the meal in English.\nHint from the user: it's lasagne",
            MealPicturePrompt.Instruction("en", "  it's lasagne \n"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Instruction_IgnoresABlankHint(string? hint) =>
        Assert.Equal("Write the meal in English.", MealPicturePrompt.Instruction("en", hint));
}
