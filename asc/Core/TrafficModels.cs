namespace ACLocalTrafficStudio;

/// <summary>AI 行為設定（全部為 0..1 的正規化數值，速度係數與跟車距離另有合理範圍）。</summary>
public sealed class AiBehaviorSettings
{
    /// <summary>侵略性：0 = 膽小跟車，1 = 激進超車。</summary>
    public double Aggression { get; set; } = 0.5;

    /// <summary>速度係數（相對車道速限）。</summary>
    public double SpeedMultiplier { get; set; } = 1.0;

    /// <summary>變道頻率。</summary>
    public double LaneChangeFrequency { get; set; } = 0.5;

    /// <summary>跟車距離係數（1 = 標準車距）。</summary>
    public double FollowDistanceFactor { get; set; } = 1.0;

    /// <summary>超車傾向。</summary>
    public double OvertakeBias { get; set; } = 0.5;

    /// <summary>遇事故減速圍觀（rubbernecking）機率。</summary>
    public double RubberneckProbability { get; set; } = 0.05;

    public AiBehaviorSettings Clone() => new()
    {
        Aggression = Aggression,
        SpeedMultiplier = SpeedMultiplier,
        LaneChangeFrequency = LaneChangeFrequency,
        FollowDistanceFactor = FollowDistanceFactor,
        OvertakeBias = OvertakeBias,
        RubberneckProbability = RubberneckProbability
    };

    public static void Clamp(AiBehaviorSettings b)
    {
        b.Aggression = System.Math.Clamp(b.Aggression, 0, 1);
        b.SpeedMultiplier = System.Math.Clamp(b.SpeedMultiplier, 0.5, 1.5);
        b.LaneChangeFrequency = System.Math.Clamp(b.LaneChangeFrequency, 0, 1);
        b.FollowDistanceFactor = System.Math.Clamp(b.FollowDistanceFactor, 0.3, 3.0);
        b.OvertakeBias = System.Math.Clamp(b.OvertakeBias, 0, 1);
        b.RubberneckProbability = System.Math.Clamp(b.RubberneckProbability, 0, 1);
    }

    /// <summary>
    /// 行為解析優先順序：個別車輛覆寫 ＞ 級別覆寫 ＞ 預設檔基礎行為 ＞ 全域預設。
    /// 每個欄位單獨 fallback，未覆寫的欄位繼承下一層。
    /// </summary>
    public static AiBehaviorSettings Resolve(
        BehaviorOverride? carOverride,
        BehaviorOverride? classOverride,
        AiBehaviorSettings? profileBase,
        AiBehaviorSettings? globalFallback)
    {
        var b = profileBase ?? globalFallback ?? new AiBehaviorSettings();
        return new AiBehaviorSettings
        {
            Aggression = carOverride?.Aggression ?? classOverride?.Aggression ?? b.Aggression,
            SpeedMultiplier = carOverride?.SpeedMultiplier ?? classOverride?.SpeedMultiplier ?? b.SpeedMultiplier,
            LaneChangeFrequency = carOverride?.LaneChangeFrequency ?? classOverride?.LaneChangeFrequency ?? b.LaneChangeFrequency,
            FollowDistanceFactor = carOverride?.FollowDistanceFactor ?? classOverride?.FollowDistanceFactor ?? b.FollowDistanceFactor,
            OvertakeBias = carOverride?.OvertakeBias ?? classOverride?.OvertakeBias ?? b.OvertakeBias,
            RubberneckProbability = carOverride?.RubberneckProbability ?? classOverride?.RubberneckProbability ?? b.RubberneckProbability
        };
    }
}

/// <summary>部分欄位的行為覆寫（null = 不覆寫，繼承下一層）。</summary>
public sealed class BehaviorOverride
{
    public double? Aggression { get; set; }
    public double? SpeedMultiplier { get; set; }
    public double? LaneChangeFrequency { get; set; }
    public double? FollowDistanceFactor { get; set; }
    public double? OvertakeBias { get; set; }
    public double? RubberneckProbability { get; set; }

    public bool IsEmpty =>
        Aggression is null && SpeedMultiplier is null && LaneChangeFrequency is null &&
        FollowDistanceFactor is null && OvertakeBias is null && RubberneckProbability is null;

    public BehaviorOverride Clone() => new()
    {
        Aggression = Aggression,
        SpeedMultiplier = SpeedMultiplier,
        LaneChangeFrequency = LaneChangeFrequency,
        FollowDistanceFactor = FollowDistanceFactor,
        OvertakeBias = OvertakeBias,
        RubberneckProbability = RubberneckProbability
    };

    public static string F(double? v) => v.HasValue ? v.Value.ToString("F2") : "—";
}

/// <summary>交通預設檔：描述一種交通情境（高速公路 / 賽道日 / 自訂）。</summary>
public sealed class TrafficProfile
{
    /// <summary>內建預設檔的固定 Id。</summary>
    public static class Ids
    {
        public const string ShutokoHighway = "builtin-shutoko-highway";
        public const string NordschleifeTrackDay = "builtin-nordschleife-trackday";
        public const string UrbanStreet = "builtin-urban-street";
        public const string NightCruise = "builtin-night-cruise";
    }

    public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "自訂預設檔";
    public string Description { get; set; } = "";
    public ProfileKind Kind { get; set; } = ProfileKind.Custom;
    public bool IsBuiltIn { get; set; }

    /// <summary>密度 100% 時的 AI 車數上限。</summary>
    public int MaxAiCars { get; set; } = 40;

    /// <summary>車道數（高速公路多車道；賽道日通常 1）。</summary>
    public int Lanes { get; set; } = 3;

    /// <summary>每台車的速度係數隨機範圍。</summary>
    public double MinSpeedMultiplier { get; set; } = 0.9;
    public double MaxSpeedMultiplier { get; set; } = 1.1;

    /// <summary>生成點距離玩家的範圍（公尺）。</summary>
    public double SpawnAheadMinM { get; set; } = 400;
    public double SpawnAheadMaxM { get; set; } = 1500;

    /// <summary>地圖長度未知時使用的估計長度（公里）。</summary>
    public double DefaultTrackLengthKm { get; set; } = 20.0;

    /// <summary>只允許含有這些標籤的車輛（逗號分隔；空白 = 全部允許）。</summary>
    public string AllowedTags { get; set; } = "";

    /// <summary>排除含有這些標籤的車輛（逗號分隔）。</summary>
    public string ExcludedTags { get; set; } = "formula";

    /// <summary>此預設檔的基礎 AI 行為。</summary>
    public AiBehaviorSettings BaseBehavior { get; set; } = new();

    /// <summary>級別（class）層級的行為覆寫。</summary>
    public System.Collections.Generic.Dictionary<string, BehaviorOverride> ClassOverrides { get; set; } = new();

    /// <summary>個別車輛（資料夾名稱）層級的行為覆寫。</summary>
    public System.Collections.Generic.Dictionary<string, BehaviorOverride> CarOverrides { get; set; } = new();

    public static List<TrafficProfile> BuiltIns() => new()
    {
        ShutokoHighway(),
        NordschleifeTrackDay(),
        UrbanStreet(),
        NightCruise()
    };

    /// <summary>🛣️ Shutoko 高速公路車流：高密度、多車道、頻繁變道、中等侵略性。</summary>
    public static TrafficProfile ShutokoHighway() => new()
    {
        Id = Ids.ShutokoHighway,
        Name = "Shutoko 高速公路車流",
        IsBuiltIn = true,
        Kind = ProfileKind.Highway,
        Description = "適用於 Shutoko Revival Project 等高速公路地圖：高密度、多車道、頻繁變道、速度略高於車流。",
        MaxAiCars = 60,
        Lanes = 3,
        MinSpeedMultiplier = 0.90,
        MaxSpeedMultiplier = 1.15,
        SpawnAheadMinM = 500,
        SpawnAheadMaxM = 1800,
        DefaultTrackLengthKm = 32.0,
        AllowedTags = "",
        ExcludedTags = "formula",
        BaseBehavior = new AiBehaviorSettings
        {
            Aggression = 0.45,
            SpeedMultiplier = 1.00,
            LaneChangeFrequency = 0.70,
            FollowDistanceFactor = 1.10,
            OvertakeBias = 0.60,
            RubberneckProbability = 0.08
        }
    };

    /// <summary>🏁 Nordschleife 賽道日：低密度、高侵略性、單線為主、直道才超車。</summary>
    public static TrafficProfile NordschleifeTrackDay() => new()
    {
        Id = Ids.NordschleifeTrackDay,
        Name = "Nordschleife 賽道日",
        IsBuiltIn = true,
        Kind = ProfileKind.TrackDay,
        Description = "適用於 Nordschleife / Touristenfahrten：低密度、單線跟馳、高侵略性但速度保守。",
        MaxAiCars = 18,
        Lanes = 1,
        MinSpeedMultiplier = 0.75,
        MaxSpeedMultiplier = 1.00,
        SpawnAheadMinM = 200,
        SpawnAheadMaxM = 900,
        DefaultTrackLengthKm = 20.8,
        AllowedTags = "",
        ExcludedTags = "formula",
        BaseBehavior = new AiBehaviorSettings
        {
            Aggression = 0.70,
            SpeedMultiplier = 0.90,
            LaneChangeFrequency = 0.20,
            FollowDistanceFactor = 1.40,
            OvertakeBias = 0.80,
            RubberneckProbability = 0.00
        }
    };

    /// <summary>都市街道巡航：中密度、雙車道、保守行為。</summary>
    public static TrafficProfile UrbanStreet() => new()
    {
        Id = Ids.UrbanStreet,
        Name = "都市街道巡航",
        IsBuiltIn = true,
        Kind = ProfileKind.Custom,
        Description = "適用於一般街道地圖：中密度、雙車道、禮讓行人般的保守車流。",
        MaxAiCars = 30,
        Lanes = 2,
        MinSpeedMultiplier = 0.85,
        MaxSpeedMultiplier = 1.05,
        SpawnAheadMinM = 300,
        SpawnAheadMaxM = 1000,
        DefaultTrackLengthKm = 8.0,
        AllowedTags = "",
        ExcludedTags = "formula,race",
        BaseBehavior = new AiBehaviorSettings
        {
            Aggression = 0.35,
            SpeedMultiplier = 0.95,
            LaneChangeFrequency = 0.50,
            FollowDistanceFactor = 1.20,
            OvertakeBias = 0.35,
            RubberneckProbability = 0.10
        }
    };

    /// <summary>深夜稀疏車流：低密度、低侵略性。</summary>
    public static TrafficProfile NightCruise() => new()
    {
        Id = Ids.NightCruise,
        Name = "深夜稀疏車流",
        IsBuiltIn = true,
        Kind = ProfileKind.Custom,
        Description = "深夜時段：車輛稀少、安靜跟車、幾乎不變道。",
        MaxAiCars = 12,
        Lanes = 2,
        MinSpeedMultiplier = 0.90,
        MaxSpeedMultiplier = 1.05,
        SpawnAheadMinM = 400,
        SpawnAheadMaxM = 1400,
        DefaultTrackLengthKm = 10.0,
        AllowedTags = "",
        ExcludedTags = "formula,race",
        BaseBehavior = new AiBehaviorSettings
        {
            Aggression = 0.25,
            SpeedMultiplier = 0.95,
            LaneChangeFrequency = 0.20,
            FollowDistanceFactor = 1.30,
            OvertakeBias = 0.20,
            RubberneckProbability = 0.02
        }
    };
}

/// <summary>本機環境掃描結果。</summary>
public sealed class EnvironmentInfo
{
    public string AcRoot { get; set; } = "";
    public string AcVersion { get; set; } = "";
    public bool HasCustomShadersPatch { get; set; }
    public bool HasContentManager { get; set; }
    public string DocumentsFolder { get; set; } = "";
    public bool DocumentsFolderExists { get; set; }
    public int CarCount { get; set; }
    public int TrackCount { get; set; }
    public int SuitableCarCount { get; set; }
    public bool HasShutoko { get; set; }
    public bool HasNordschleife { get; set; }
    public System.Collections.Generic.List<string> Notes { get; set; } = new();
}

/// <summary>工作室級別的設定（作用中預設檔、全域行為、密度、每張地圖的長度）。</summary>
public sealed class StudioSettings
{
    public string ActiveProfileId { get; set; } = TrafficProfile.Ids.ShutokoHighway;
    public AiBehaviorSettings GlobalBehavior { get; set; } = new()
    {
        Aggression = 0.5,
        SpeedMultiplier = 1.0,
        LaneChangeFrequency = 0.5,
        FollowDistanceFactor = 1.0,
        OvertakeBias = 0.5,
        RubberneckProbability = 0.05
    };
    public double TargetDensity { get; set; } = 50;
    public double RampPerSecond { get; set; } = 2.0;
    public bool ImmediateDensity { get; set; }
    public string DensitySchedule { get; set; } = "constant";

    /// <summary>每張地圖/Layout 手動設定的長度（key = TrackEntry.Key）。</summary>
    public System.Collections.Generic.Dictionary<string, double> TrackLengthsKm { get; set; } = new();
}

/// <summary>密度排程中的一個時間點。</summary>
public sealed class DensitySchedulePoint
{
    public double AtSeconds { get; set; }
    public double Density { get; set; }
}

/// <summary>計畫中的單一台 AI 交通車。</summary>
public sealed class PlannedVehicle
{
    public int Index { get; set; }
    public string CarFolder { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Brand { get; set; } = "";
    public double SpawnDistanceM { get; set; }
    public int Lane { get; set; }
    public double SpeedMultiplier { get; set; }
    public AiBehaviorSettings Behavior { get; set; } = new();
    public string Note { get; set; } = "";
}

/// <summary>一份完整的交通計畫（可匯出為唯讀 JSON）。</summary>
public sealed class TrafficPlan
{
    public int SchemaVersion { get; set; } = 2;
    public bool Offline { get; set; } = true;
    public System.DateTime GeneratedAt { get; set; } = System.DateTime.Now;
    public string AcRoot { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public string TrackFolder { get; set; } = "";
    public string TrackLayout { get; set; } = "";
    public string TrackDisplayName { get; set; } = "";
    public string MapKind { get; set; } = "Generic";
    public double TrackLengthKm { get; set; }
    public double Density { get; set; }
    public int VehicleCount { get; set; }
    public int Lanes { get; set; }
    public double EffectiveDensityPerKm => TrackLengthKm > 0 ? VehicleCount / TrackLengthKm : 0;
    public System.Collections.Generic.List<PlannedVehicle> Vehicles { get; set; } = new();
    public System.Collections.Generic.List<string> Warnings { get; set; } = new();
}

/// <summary>「AI 行為管理」分頁中級別覆寫列表的顯示列。</summary>
public sealed class ClassOverrideRow
{
    public string ClassName { get; set; } = "";
    public string Aggression { get; set; } = "";
    public string SpeedMultiplier { get; set; } = "";
    public string LaneChangeFrequency { get; set; } = "";
    public BehaviorOverride Source { get; set; } = new();
}
