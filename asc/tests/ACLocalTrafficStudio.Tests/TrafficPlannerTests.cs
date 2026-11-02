using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ACLocalTrafficStudio.Tests;

public class TrafficPlannerTests
{
    private static List<CarEntry> FakeCars(int n) => Enumerable.Range(0, n)
        .Select(i => new CarEntry
        {
            Folder = "car" + i,
            DisplayName = "Car " + i,
            Brand = "Brand",
            ClassName = "street",
            Tags = "street",
            HasPhysics = true,
            Suitability = TrafficSuitability.Possible
        })
        .ToList();

    private static TrackEntry ShutokoTrack() => new()
    {
        Folder = "shutoko_revival_project",
        Layout = "",
        DisplayName = "Shutoko Revival Project",
        Kind = MapKind.HighwayShutoko,
        LengthKm = 32
    };

    [Fact]
    public void Build_RespectsDensity_AndPoolLimit()
    {
        var p = TrafficProfile.ShutokoHighway(); // MaxAiCars = 60
        var plan = TrafficPlanner.Build(p, ShutokoTrack(), 50, FakeCars(100), seed: 42);
        Assert.Equal(30, plan.VehicleCount);
        Assert.Equal(30, plan.Vehicles.Count);
        Assert.All(plan.Vehicles, v => Assert.InRange(v.Lane, 0, p.Lanes - 1));
        Assert.All(plan.Vehicles, v => Assert.InRange(v.SpeedMultiplier, p.MinSpeedMultiplier, p.MaxSpeedMultiplier));
        Assert.All(plan.Vehicles, v => Assert.InRange(v.SpawnDistanceM, 0, 32000));
        Assert.True(plan.Offline);
        Assert.Equal("HighwayShutoko", plan.MapKind);
    }

    [Fact]
    public void Build_CapsAtPoolSize_WithWarning()
    {
        var p = TrafficProfile.ShutokoHighway();
        var plan = TrafficPlanner.Build(p, ShutokoTrack(), 100, FakeCars(10), seed: 1);
        Assert.Equal(10, plan.VehicleCount);
        Assert.Contains(plan.Warnings, w => w.Contains("車輛池"));
    }

    [Fact]
    public void Build_EmptyPool_ProducesEmptyPlan_WithWarning()
    {
        var p = TrafficProfile.NordschleifeTrackDay();
        var plan = TrafficPlanner.Build(p, ShutokoTrack(), 80, new List<CarEntry>(), seed: 1);
        Assert.Equal(0, plan.VehicleCount);
        Assert.NotEmpty(plan.Warnings);
    }

    [Fact]
    public void Build_UsesDefaultLength_WhenUnknown()
    {
        var p = TrafficProfile.NordschleifeTrackDay();
        var track = ShutokoTrack();
        track.LengthKm = 0;
        var plan = TrafficPlanner.Build(p, track, 50, FakeCars(50), seed: 7);
        Assert.Equal(p.DefaultTrackLengthKm, plan.TrackLengthKm);
        Assert.Contains(plan.Warnings, w => w.Contains("長度未知"));
    }

    [Fact]
    public void Build_ResolvesCarOverride_OverProfileBase()
    {
        var p = TrafficProfile.ShutokoHighway();
        p.CarOverrides["car3"] = new BehaviorOverride { Aggression = 0.99 };
        var cars = FakeCars(5);
        var plan = TrafficPlanner.Build(p, ShutokoTrack(), 100, cars, seed: 3);
        // 密度 100% 且車池 5 部 → 全部上車；car3 必在其中且侵略性為覆寫值
        var v = plan.Vehicles.First(x => x.CarFolder == "car3");
        Assert.Equal(0.99, v.Behavior.Aggression, 3);
        // 其他車繼承預設檔基礎行為
        var other = plan.Vehicles.First(x => x.CarFolder != "car3");
        Assert.Equal(p.BaseBehavior.Aggression, other.Behavior.Aggression, 3);
    }

    [Fact]
    public void SelectPool_ExcludesRaceOnlyAndExcludedTags()
    {
        var p = TrafficProfile.ShutokoHighway(); // ExcludedTags = formula
        var cars = new List<CarEntry>
        {
            new() { Folder = "a", HasPhysics = true, Suitability = TrafficSuitability.Good, Tags = "street" },
            new() { Folder = "b", HasPhysics = true, Suitability = TrafficSuitability.RaceOnly, Tags = "race" },
            new() { Folder = "c", HasPhysics = true, Suitability = TrafficSuitability.Possible, Tags = "formula" },
            new() { Folder = "d", HasPhysics = false, Suitability = TrafficSuitability.Incomplete, Tags = "street" }
        };
        var pool = TrafficPlanner.SelectPool(p, cars);
        Assert.Single(pool);
        Assert.Equal("a", pool[0].Folder);
    }
}
