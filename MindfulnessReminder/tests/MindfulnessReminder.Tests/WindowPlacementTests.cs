using MindfulnessReminder.Core;

namespace MindfulnessReminder.Tests;

public class WindowPlacementTests
{
    [Fact]
    public void Centers_the_window_in_the_work_area()
    {
        var (left, top) = WindowPlacement.CenterIn(new WorkArea(0, 0, 1920, 1080), 440, 260);

        Assert.Equal(740, left);
        Assert.Equal(410, top);
    }

    [Fact]
    public void Centers_on_a_secondary_monitor_using_its_virtual_desktop_offset()
    {
        var (left, top) = WindowPlacement.CenterIn(new WorkArea(1920, 0, 1920, 1040), 440, 260);

        Assert.Equal(2660, left);
        Assert.Equal(390, top);
    }

    [Fact]
    public void Keeps_the_window_on_screen_when_it_is_wider_than_the_work_area()
    {
        var (left, top) = WindowPlacement.CenterIn(new WorkArea(100, 50, 300, 200), 440, 260);

        Assert.Equal(100, left);
        Assert.Equal(50, top);
    }
}
