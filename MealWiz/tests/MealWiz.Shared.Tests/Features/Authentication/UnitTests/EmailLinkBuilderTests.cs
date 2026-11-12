using MealWiz.Shared.Features.Authentication.EmailLinks;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace MealWiz.Shared.Tests.Features.Authentication.UnitTests;

public class EmailLinkBuilderTests
{
    private sealed class FakeNavigation : NavigationManager
    {
        public FakeNavigation() => Initialize("https://app.example.com/", "https://app.example.com/account");
    }

    [Fact]
    public void Web_links_to_its_own_base_uri()
    {
        var builder = new EmailLinkBuilder(new FakeNavigation(), new EmailLinkOptions(null));

        Assert.Equal("https://app.example.com/reset-password", builder.Build("reset-password"));
        Assert.False(builder.OpensInBrowser);
    }

    [Theory]
    [InlineData("https://web.example.com")]
    [InlineData("https://web.example.com/")]
    public void Maui_links_to_the_configured_web_app_with_one_slash(string webAppUrl)
    {
        var builder = new EmailLinkBuilder(new FakeNavigation(), new EmailLinkOptions(webAppUrl));

        Assert.Equal("https://web.example.com/email-change", builder.Build("email-change"));
        Assert.Equal("https://web.example.com/email-change", builder.Build("/email-change"));
        Assert.True(builder.OpensInBrowser);
    }
}
