using System;

namespace ACLocalTrafficStudio;

/// <summary>
/// 依地圖資料夾 / Layout 名稱，將地圖分類為「高速公路（Shutoko）」或「賽道日（Nordschleife）」。
/// 純本地啟發式分類，不連線、不讀取遊戲以外的資料。
/// </summary>
public static class MapClassifier
{
    public static MapKind Classify(string? trackFolder, string? layout)
    {
        var s = ((trackFolder ?? "") + " " + (layout ?? "")).ToLowerInvariant();

        // Shutoko Revival Project 等高速公路地圖
        if (s.Contains("shutoko") || s.Contains("srp"))
            return MapKind.HighwayShutoko;

        // Nordschleife / Nürburgring 北環（排除 GP 等大獎賽場地）
        if (s.Contains("nordschleife"))
            return MapKind.TrackDayNordschleife;

        if ((s.Contains("nurburgring") || s.Contains("nürburgring") || s.Contains("nurburg"))
            && !s.Contains("gp") && !s.Contains("grand") && !s.Contains("24h"))
            return MapKind.TrackDayNordschleife;

        return MapKind.Generic;
    }

    /// <summary>依地圖類型建議對應的內建預設檔 Id。</summary>
    public static string SuggestedProfileId(MapKind kind) => kind switch
    {
        MapKind.HighwayShutoko => TrafficProfile.Ids.ShutokoHighway,
        MapKind.TrackDayNordschleife => TrafficProfile.Ids.NordschleifeTrackDay,
        _ => TrafficProfile.Ids.UrbanStreet
    };
}
