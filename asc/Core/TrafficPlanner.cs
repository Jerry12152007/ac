using System;
using System.Collections.Generic;
using System.Linq;

namespace ACLocalTrafficStudio;

/// <summary>
/// 交通計畫產生器：依預設檔 + 地圖 + 密度，從掃描到的車輛池中挑選車輛，
/// distribute 生成位置與車道，並解析每台車的最終 AI 行為。
/// 完全離線、唯讀：只讀取掃描結果，不寫入任何檔案。
/// </summary>
public static class TrafficPlanner
{
    public static TrafficPlan Build(
        TrafficProfile profile,
        TrackEntry track,
        double density,
        IReadOnlyList<CarEntry> cars,
        string acRoot = "",
        int seed = 0)
    {
        var warnings = new List<string>();
        var rng = seed == 0 ? new Random() : new Random(seed);

        var pool = SelectPool(profile, cars, warnings);

        var lengthKm = track.LengthKm > 0 ? track.LengthKm : profile.DefaultTrackLengthKm;
        if (track.LengthKm <= 0)
            warnings.Add($"此地圖長度未知，使用預設檔估計值 {profile.DefaultTrackLengthKm:F1} km（可在「地圖與 Layout」分頁手動設定實際長度）。");
        var lengthM = Math.Max(lengthKm * 1000.0, 500.0);

        var densityClamped = DensityController.Clamp(density);
        var requested = (int)Math.Round(profile.MaxAiCars * densityClamped / 100.0);
        var count = Math.Min(requested, pool.Count);

        if (pool.Count == 0)
            warnings.Add("找不到合適的交通車輛（請確認車輛具備 data/ 或 data.acd，且標籤未被預設檔排除）。計畫為空。");
        else if (count < requested)
            warnings.Add($"車輛池只有 {pool.Count} 部，少於密度 {densityClamped:F0}% 所要求的 {requested} 部，將使用全部 {pool.Count} 部。");

        var plan = new TrafficPlan
        {
            SchemaVersion = 2,
            Offline = true,
            GeneratedAt = DateTime.Now,
            AcRoot = acRoot,
            ProfileId = profile.Id,
            ProfileName = profile.Name,
            TrackFolder = track.Folder,
            TrackLayout = track.Layout,
            TrackDisplayName = track.DisplayName,
            MapKind = track.Kind.ToString(),
            TrackLengthKm = lengthKm,
            Density = densityClamped,
            VehicleCount = count,
            Lanes = Math.Max(1, profile.Lanes),
            Warnings = warnings
        };

        var spacing = lengthM / Math.Max(count, 1);
        var speedSpan = Math.Max(0.0001, profile.MaxSpeedMultiplier - profile.MinSpeedMultiplier);

        for (var i = 0; i < count; i++)
        {
            var car = pool[rng.Next(pool.Count)];

            var carOverride = profile.CarOverrides.TryGetValue(car.Folder, out var co) ? co : null;
            var classOverride = !string.IsNullOrWhiteSpace(car.ClassName) &&
                                profile.ClassOverrides.TryGetValue(car.ClassName, out var clo)
                ? clo
                : null;
            var behavior = AiBehaviorSettings.Resolve(carOverride, classOverride, profile.BaseBehavior, null);

            var lane = profile.Kind == ProfileKind.Highway && plan.Lanes > 1
                ? rng.Next(plan.Lanes)
                : 0;

            var speed = profile.MinSpeedMultiplier + rng.NextDouble() * speedSpan;

            // 均勻分布在整條賽道上，加一點隨機擾動，避免車輛排成一列
            var spawn = (i + 0.5) * spacing + (rng.NextDouble() - 0.5) * spacing * 0.4;
            spawn = Math.Clamp(spawn, 0, lengthM);

            plan.Vehicles.Add(new PlannedVehicle
            {
                Index = i + 1,
                CarFolder = car.Folder,
                DisplayName = car.DisplayName,
                Brand = car.Brand,
                SpawnDistanceM = Math.Round(spawn, 1),
                Lane = lane,
                SpeedMultiplier = Math.Round(speed, 3),
                Behavior = behavior
            });
        }

        return plan;
    }

    /// <summary>
    /// 依預設檔的標籤過濾規則，從掃描結果中挑出交通車輛池。
    /// 若過濾後為空，會放寬為「所有具備物理資料且非賽車專用」的車輛，並記錄警告。
    /// </summary>
    public static List<CarEntry> SelectPool(TrafficProfile profile, IReadOnlyList<CarEntry> cars, List<string>? warnings = null)
    {
        var allowed = SplitTags(profile.AllowedTags);
        var excluded = SplitTags(profile.ExcludedTags);

        var pool = new List<CarEntry>();
        foreach (var c in cars)
        {
            if (!c.HasPhysics || c.Suitability == TrafficSuitability.Incomplete) continue;
            if (c.Suitability == TrafficSuitability.RaceOnly) continue;

            var tags = SplitTags(c.Tags);
            if (excluded.Count > 0 && tags.Any(t => excluded.Contains(t))) continue;
            if (allowed.Count > 0 && !tags.Any(t => allowed.Contains(t))) continue;
            pool.Add(c);
        }

        if (pool.Count == 0)
        {
            pool = cars
                .Where(c => c.HasPhysics && c.Suitability != TrafficSuitability.Incomplete)
                .ToList();
            warnings?.Add("標籤過濾後沒有可用車輛，已放寬為所有具備物理資料的車輛。");
        }
        return pool;
    }

    private static HashSet<string> SplitTags(string? raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw)) return set;
        foreach (var part in raw.Split(',', ';', '|', '，', '；'))
        {
            var t = part.Trim().ToLowerInvariant();
            if (t.Length > 0) set.Add(t);
        }
        return set;
    }
}
