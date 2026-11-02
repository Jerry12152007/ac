using System.Linq;

namespace ACLocalTrafficStudio;

/// <summary>
/// 依 ui_car.json 的 class 與 tags，初步判斷車輛是否適合作為交通車。
/// 這只是啟發式分類，不代表實際 AI 駕駛表現（需後續實測）。
/// </summary>
public static class CarClassifier
{
    private static readonly string[] RaceTags =
        { "race", "formula", "cup", "gt3", "gt4", "prototype", "lmp", "rally", "drift" };

    private static readonly string[] StreetTags =
        { "street", "road", "production", "tuned", "suv", "van", "pickup", "kei" };

    public static TrafficSuitability Classify(string? className, string? tags, bool hasPhysics)
    {
        if (!hasPhysics) return TrafficSuitability.Incomplete;

        var t = (tags ?? "").ToLowerInvariant();
        var c = (className ?? "").ToLowerInvariant();

        if (RaceTags.Any(x => t.Contains(x) || c.Contains(x)))
            return TrafficSuitability.RaceOnly;

        if (StreetTags.Any(x => t.Contains(x) || c.Contains(x)))
            return TrafficSuitability.Good;

        return TrafficSuitability.Possible;
    }
}
