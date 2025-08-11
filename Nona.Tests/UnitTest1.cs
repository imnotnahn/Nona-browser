using FluentAssertions;
using Nona.Core;

namespace Nona.Tests;

public class TabManagerTests
{
    [Fact]
    public void NewTab_ShouldBecomeActive()
    {
        var tm = new TabManager();
        var tab = tm.NewTab("https://example.com");
        tm.ActiveTab.Should().Be(tab);
        tm.Tabs.Should().Contain(tab);
    }
}
