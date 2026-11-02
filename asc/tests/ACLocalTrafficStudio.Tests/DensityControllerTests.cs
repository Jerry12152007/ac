using System.Linq;
using Xunit;

namespace ACLocalTrafficStudio.Tests;

public class DensityControllerTests
{
    [Fact]
    public void Tick_MovesTowardTarget_AndStopsAtTarget()
    {
        var c = new DensityController(0) { RampPerSecond = 10 };
        c.SetTarget(50);
        c.Tick(1);
        Assert.Equal(10, c.CurrentDensity, 3);
        for (var i = 0; i < 20; i++) c.Tick(1);
        Assert.True(c.IsAtTarget);
        Assert.Equal(50, c.CurrentDensity, 3);
    }

    [Fact]
    public void Immediate_AppliesAtOnce()
    {
        var c = new DensityController(10) { Immediate = true };
        c.SetTarget(80);
        Assert.Equal(80, c.CurrentDensity);
    }

    [Fact]
    public void Target_IsClampedTo0_100()
    {
        var c = new DensityController();
        c.SetTarget(150);
        Assert.Equal(100, c.TargetDensity);
        c.SetTarget(-5);
        Assert.Equal(0, c.TargetDensity);
    }

    [Fact]
    public void CarCount_ScalesWithDensity()
    {
        var c = new DensityController(50);
        Assert.Equal(30, c.TargetCarCount(60));
        Assert.Equal(0, c.TargetCarCount(0));
        var c0 = new DensityController(0);
        Assert.Equal(0, c0.TargetCarCount(60));
        var c100 = new DensityController(100);
        Assert.Equal(60, c100.TargetCarCount(60));
    }

    [Fact]
    public void Schedule_CoversDuration_AndStaysInRange()
    {
        var pts = DensityController.BuildSchedule("rush_hour", 1000, 50);
        Assert.True(pts.Count >= 2);
        Assert.Equal(0, pts.First().AtSeconds);
        Assert.Equal(1000, pts.Last().AtSeconds);
        Assert.All(pts, p => Assert.InRange(p.Density, 0, 100));
    }

    [Fact]
    public void ConstantSchedule_UsesGivenLevel()
    {
        var pts = DensityController.BuildSchedule("constant", 600, 42);
        Assert.All(pts, p => Assert.Equal(42, p.Density));
    }
}
