using MealWiz.Shared.Services.DrawerStateContainer;
using Microsoft.AspNetCore.Components;
using Xunit;
using DrawerStateContainerService = MealWiz.Shared.Services.DrawerStateContainer.DrawerStateContainer;

namespace MealWiz.Shared.Tests.Services.DrawerStateContainer.UnitTests;

public class DrawerStateContainerTests
{
    private sealed class FirstPage;
    private sealed class SecondPage;
    private sealed class ThirdPage;

    private readonly DrawerStateContainerService _container = new();
    private int _notifications;

    public DrawerStateContainerTests()
    {
        _container.OnDrawerChange += () => _notifications++;
    }

    [Fact]
    public void OpenDrawer_WithTitle_SetsContentAndOpens()
    {
        _container.OpenDrawer("Edit meal", typeof(FirstPage));

        Assert.True(_container.IsDrawerOpen);
        Assert.Equal("Edit meal", _container.StateParameters.Title);
        Assert.Equal(typeof(FirstPage), _container.StateParameters.Content);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public void OpenDrawer_WithHeader_SetsHeaderAndOpens()
    {
        RenderFragment header = _ => { };

        _container.OpenDrawer(header, typeof(FirstPage));

        Assert.True(_container.IsDrawerOpen);
        Assert.Same(header, _container.StateParameters.Header);
        Assert.Equal(typeof(FirstPage), _container.StateParameters.Content);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public void OpenDrawer_WithParameters_UsesThemAndOpens()
    {
        var parameters = new DrawerStateParameters { Title = "Import", Content = typeof(FirstPage), Width = "400px" };

        _container.OpenDrawer(parameters);

        Assert.True(_container.IsDrawerOpen);
        Assert.Same(parameters, _container.StateParameters);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public void ClosingTheDrawer_ResetsItsContentButKeepsItsSize()
    {
        _container.OpenDrawer(new DrawerStateParameters { Title = "Import", Content = typeof(FirstPage), Width = "400px", Height = "50%" });
        _container.SwitchDrawerContent(typeof(SecondPage), "Second");

        _container.IsDrawerOpen = false;

        Assert.Null(_container.StateParameters.Title);
        Assert.Null(_container.StateParameters.Content);
        Assert.Equal("400px", _container.StateParameters.Width);
        Assert.Equal("50%", _container.StateParameters.Height);
        Assert.Empty(_container.DrawerNavigationStack);
    }

    [Fact]
    public void SwitchDrawerContent_ReplacesTheContentAndPushesASnapshot()
    {
        _container.OpenDrawer("First", typeof(FirstPage));
        _notifications = 0;
        var parameters = new Dictionary<string, object> { ["MealId"] = 7 };

        _container.SwitchDrawerContent(typeof(SecondPage), "Second", parameters);

        Assert.Equal(typeof(SecondPage), _container.StateParameters.Content);
        Assert.Equal("Second", _container.StateParameters.Title);
        Assert.Same(parameters, _container.StateParameters.ContentParameters);
        var snapshot = Assert.Single(_container.DrawerNavigationStack);
        Assert.NotSame(_container.StateParameters, snapshot);
        Assert.Equal(typeof(SecondPage), snapshot.Content);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public void SwitchDrawerContent_WithoutParameters_UsesAnEmptyDictionary()
    {
        _container.SwitchDrawerContent(typeof(SecondPage));

        Assert.NotNull(_container.StateParameters.ContentParameters);
        Assert.Empty(_container.StateParameters.ContentParameters);
        Assert.Equal("", _container.StateParameters.Title);
    }

    [Fact]
    public void NavigateBack_RestoresThePreviousContent()
    {
        var firstParameters = new Dictionary<string, object> { ["MealId"] = 7 };
        _container.SwitchDrawerContent(typeof(FirstPage), "First", firstParameters);
        _container.SwitchDrawerContent(typeof(SecondPage), "Second");
        _container.SwitchDrawerContent(typeof(ThirdPage), "Third");
        _notifications = 0;

        _container.NavigateBack();

        Assert.Equal(typeof(SecondPage), _container.StateParameters.Content);
        Assert.Equal("Second", _container.StateParameters.Title);
        Assert.Equal(2, _container.DrawerNavigationStack.Count);
        Assert.Equal(1, _notifications);

        _container.NavigateBack();

        Assert.Equal(typeof(FirstPage), _container.StateParameters.Content);
        Assert.Equal("First", _container.StateParameters.Title);
        Assert.Same(firstParameters, _container.StateParameters.ContentParameters);
    }

    [Fact]
    public void NavigateBack_OnTheFirstPage_DoesNothing()
    {
        _container.SwitchDrawerContent(typeof(FirstPage), "First");
        _notifications = 0;

        _container.NavigateBack();

        Assert.Equal(typeof(FirstPage), _container.StateParameters.Content);
        Assert.Single(_container.DrawerNavigationStack);
        Assert.Equal(0, _notifications);
    }

    [Fact]
    public void NavigateBack_WithAnEmptyStack_DoesNothing()
    {
        _container.NavigateBack();

        Assert.Equal(0, _notifications);
    }

    [Fact]
    public void OpenConfirmDrawer_SetsTitleAndDescriptionAndOpens()
    {
        _container.OpenConfirmDrawer("Delete meal?", "This can't be undone.");

        Assert.True(_container.IsConfirmDrawerOpen);
        Assert.False(_container.IsDrawerOpen);
        Assert.Equal("Delete meal?", _container.StateParameters.Title);
        Assert.Equal("This can't be undone.", _container.StateParameters.Description);
        Assert.Equal(1, _notifications);
    }

    [Fact]
    public void CloseDrawer_ClosesBothDrawersAndNotifies()
    {
        _container.OpenDrawer("Edit meal", typeof(FirstPage));
        _container.OpenConfirmDrawer("Delete meal?", "Sure?");
        _notifications = 0;

        _container.CloseDrawer();

        Assert.False(_container.IsDrawerOpen);
        Assert.False(_container.IsConfirmDrawerOpen);
        Assert.Null(_container.StateParameters.Title);
        Assert.True(_notifications > 0);
    }

    [Fact]
    public void NotifyConfirmClicked_InvokesTheConfirmHandler()
    {
        var clicked = 0;
        _container.OnConfirmClick += () =>
        {
            clicked++;
            return Task.CompletedTask;
        };

        _container.NotifyConfirmClicked();

        Assert.Equal(1, clicked);
    }

    [Fact]
    public void NotifyConfirmClicked_WithoutAHandler_DoesNotThrow()
    {
        _container.NotifyConfirmClicked();
    }
}
