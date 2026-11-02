using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ACLocalTrafficStudio.Tests;

public class ProfileStoreTests
{
    private static string TempDir() => Path.Combine(Path.GetTempPath(), "aclts_tests_" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void MissingFile_ReturnsBuiltIns()
    {
        var loaded = ProfileStore.LoadProfiles(Path.Combine(TempDir(), "profiles.json"));
        Assert.Equal(TrafficProfile.BuiltIns().Count, loaded.Count);
        Assert.Contains(loaded, p => p.Id == TrafficProfile.Ids.ShutokoHighway);
        Assert.Contains(loaded, p => p.Id == TrafficProfile.Ids.NordschleifeTrackDay);
    }

    [Fact]
    public void SaveLoad_RoundTrips_ProfilesAndSettings()
    {
        var dir = TempDir();
        var pFile = Path.Combine(dir, "profiles.json");
        var sFile = Path.Combine(dir, "settings.json");

        var profiles = TrafficProfile.BuiltIns();
        profiles[0].MaxAiCars = 77;
        profiles[0].CarOverrides["ks_porsche_911"] = new BehaviorOverride { Aggression = 0.9 };
        ProfileStore.SaveProfiles(profiles, pFile);

        var loaded = ProfileStore.LoadProfiles(pFile);
        Assert.Equal(77, loaded.First(p => p.Id == TrafficProfile.Ids.ShutokoHighway).MaxAiCars);
        Assert.Equal(0.9, loaded.First(p => p.Id == TrafficProfile.Ids.ShutokoHighway)
            .CarOverrides["ks_porsche_911"].Aggression, 3);

        var settings = new StudioSettings { TargetDensity = 66, ActiveProfileId = TrafficProfile.Ids.NordschleifeTrackDay };
        settings.TrackLengthsKm["ks_nordschleife"] = 20.8;
        ProfileStore.SaveSettings(settings, sFile);
        var loadedSettings = ProfileStore.LoadSettings(sFile);
        Assert.Equal(66, loadedSettings.TargetDensity);
        Assert.Equal(20.8, loadedSettings.TrackLengthsKm["ks_nordschleife"], 3);
    }

    [Fact]
    public void SingleProfile_ExportImport_RoundTrips()
    {
        var dir = TempDir();
        var file = Path.Combine(dir, "p.json");
        var p = TrafficProfile.NordschleifeTrackDay();
        p.Name = "我的北環賽道日";
        ProfileStore.SaveSingleProfileFile(p, file);
        var loaded = ProfileStore.LoadSingleProfileFile(file);
        Assert.NotNull(loaded);
        Assert.Equal("我的北環賽道日", loaded!.Name);
        Assert.Equal(ProfileKind.TrackDay, loaded.Kind);
    }
}

public class ExporterTests
{
    [Fact]
    public void ExportPlan_WritesJsonAndMarkdown_WithOfflineFlag()
    {
        var dir = Path.Combine(Path.GetTempPath(), "aclts_tests_" + Guid.NewGuid().ToString("N"));
        var profile = TrafficProfile.ShutokoHighway();
        var track = new TrackEntry
        {
            Folder = "shutoko_revival_project",
            DisplayName = "SRP",
            Kind = MapKind.HighwayShutoko,
            LengthKm = 32
        };
        var cars = Enumerable.Range(0, 40).Select(i => new CarEntry
        {
            Folder = "car" + i,
            DisplayName = "Car " + i,
            HasPhysics = true,
            Suitability = TrafficSuitability.Good,
            Tags = "street"
        }).ToList();

        var plan = TrafficPlanner.Build(profile, track, 50, cars, acRoot: @"C:\AC", seed: 42);

        var jsonPath = TrafficPackageExporter.ExportPlanJson(plan, dir);
        var mdPath = TrafficPackageExporter.ExportPlanMarkdown(plan, dir);

        Assert.True(File.Exists(jsonPath));
        Assert.True(File.Exists(mdPath));
        var json = File.ReadAllText(jsonPath);
        Assert.Contains("\"Offline\": true", json);
        Assert.Contains("\"VehicleCount\": 30", json);
        var md = File.ReadAllText(mdPath);
        Assert.Contains("交通計畫摘要", md);
        Assert.Contains("| # | 車輛 | 品牌 |", md);
        Assert.Contains("Car ", md);
    }
}
