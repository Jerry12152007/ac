namespace ACLocalTrafficStudio;

/// <summary>地圖類型分類（依資料夾 / Layout 名稱啟發式判斷）。</summary>
public enum MapKind
{
    Generic,
    HighwayShutoko,
    TrackDayNordschleife
}

/// <summary>交通預設檔類型。</summary>
public enum ProfileKind
{
    Highway,
    TrackDay,
    Custom
}

/// <summary>車輛作為交通車的初步合適度（依 ui_car.json 的 class / tags 啟發式判斷）。</summary>
public enum TrafficSuitability
{
    Unknown,
    Good,
    Possible,
    RaceOnly,
    Incomplete
}

public sealed class CarEntry
{
    public string Folder { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Brand { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Tags { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string Status { get; set; } = "";
    public string Details { get; set; } = "";
    public bool HasPhysics { get; set; }
    public bool HasAiFolder { get; set; }
    public TrafficSuitability Suitability { get; set; } = TrafficSuitability.Unknown;

    public string SuitabilityText => Suitability switch
    {
        TrafficSuitability.Good => "適合交通",
        TrafficSuitability.Possible => "可能適合",
        TrafficSuitability.RaceOnly => "賽車專用",
        TrafficSuitability.Incomplete => "資料不完整",
        _ => "未知"
    };
}

public sealed class TrackEntry
{
    public string Folder { get; set; } = "";
    public string Layout { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string AiStatus { get; set; } = "";
    public MapKind Kind { get; set; } = MapKind.Generic;
    public bool HasFastLane { get; set; }

    /// <summary>軌道長度（公里）。0 = 未知，規劃時會改用預設檔的估計值。</summary>
    public double LengthKm { get; set; }

    /// <summary>穩定的唯一鍵（用於儲存每張地圖的長度設定）。</summary>
    public string Key => string.IsNullOrWhiteSpace(Layout) ? Folder : Folder + "/" + Layout;

    public string KindText => Kind switch
    {
        MapKind.HighwayShutoko => "🛣️ 高速公路（Shutoko）",
        MapKind.TrackDayNordschleife => "🏁 賽道日（Nordschleife）",
        _ => "一般賽道"
    };
}
