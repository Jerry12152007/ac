using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ACLocalTrafficStudio;

public static class AcScanner
{
    public static string? FindAcRoot()
    {
        var candidates = new List<string>();
        var drives = Environment.GetLogicalDrives();

        foreach (var drive in drives)
        {
            candidates.Add(Path.Combine(drive, "Steam", "steamapps", "common", "assettocorsa"));
            candidates.Add(Path.Combine(drive, "Program Files (x86)", "Steam", "steamapps", "common", "assettocorsa"));
            candidates.Add(Path.Combine(drive, "Program Files", "Steam", "steamapps", "common", "assettocorsa"));
            candidates.Add(Path.Combine(drive, "Games", "SteamLibrary", "steamapps", "common", "assettocorsa"));
        }

        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        candidates.Add(Path.Combine(user, "Steam", "steamapps", "common", "assettocorsa"));

        return candidates.FirstOrDefault(IsAcRoot);
    }

    public static bool IsAcRoot(string path) =>
        Directory.Exists(Path.Combine(path, "content", "cars")) &&
        Directory.Exists(Path.Combine(path, "content", "tracks"));

    public static List<CarEntry> ScanCars(string acRoot)
    {
        var root = Path.Combine(acRoot, "content", "cars");
        var result = new List<CarEntry>();
        if (!Directory.Exists(root)) return result;

        foreach (var dir in Directory.GetDirectories(root).OrderBy(Path.GetFileName))
        {
            var folder = Path.GetFileName(dir) ?? dir;
            var uiPath = Path.Combine(dir, "ui", "ui_car.json");
            var name = folder;
            var brand = "";
            var details = new List<string>();

            if (File.Exists(uiPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(uiPath));
                    var e = doc.RootElement;
                    name = ReadString(e, "name") ?? name;
                    brand = ReadString(e, "brand") ?? "";
                    var className = ReadString(e, "class");
                    var tags = ReadString(e, "tags");
                    if (!string.IsNullOrWhiteSpace(className)) details.Add("級別：" + className);
                    if (!string.IsNullOrWhiteSpace(tags)) details.Add("標籤：" + tags);
                }
                catch { details.Add("ui_car.json 無法解析"); }
            }
            else details.Add("缺少 ui/ui_car.json");

            var data = Path.Combine(dir, "data");
            var dataAcd = Path.Combine(dir, "data.acd");
            var hasPhysics = Directory.Exists(data) || File.Exists(dataAcd);
            var hasAi = Directory.Exists(Path.Combine(dir, "ai")) ||
                        Directory.Exists(Path.Combine(dir, "data", "ai"));
            if (!hasPhysics) details.Add("未找到 data/ 或 data.acd");
            if (!hasAi) details.Add("未找到獨立 AI 資料夾（不代表不能由 AC AI 使用）");

            result.Add(new CarEntry
            {
                Folder = folder,
                DisplayName = name,
                Brand = brand,
                FolderPath = dir,
                Status = hasPhysics ? "基本檔案存在・待 AI 實測" : "資料不完整・待檢查",
                Details = string.Join("；", details)
            });
        }
        return result;
    }

    public static List<TrackEntry> ScanTracks(string acRoot)
    {
        var root = Path.Combine(acRoot, "content", "tracks");
        var result = new List<TrackEntry>();
        if (!Directory.Exists(root)) return result;

        foreach (var trackDir in Directory.GetDirectories(root).OrderBy(Path.GetFileName))
        {
            var track = Path.GetFileName(trackDir) ?? trackDir;
            var layouts = Directory.GetDirectories(trackDir);
            if (layouts.Length == 0)
            {
                result.Add(MakeTrack(track, "", trackDir));
                continue;
            }

            foreach (var layoutDir in layouts)
            {
                var layout = Path.GetFileName(layoutDir) ?? "";
                result.Add(MakeTrack(track, layout, layoutDir));
            }
        }
        return result;
    }

    private static TrackEntry MakeTrack(string track, string layout, string path)
    {
        var ai = Path.Combine(path, "ai");
        var fastLane = Directory.Exists(ai) &&
            (File.Exists(Path.Combine(ai, "fast_lane.ai")) ||
             File.Exists(Path.Combine(ai, "fast_lane.aip")));
        return new TrackEntry
        {
            Folder = track,
            Layout = layout,
            DisplayName = string.IsNullOrWhiteSpace(layout) ? track : $"{track} / {layout}",
            FolderPath = path,
            AiStatus = fastLane ? "找到 fast_lane 路線檔" : "未找到 fast_lane（需進一步檢查）"
        };
    }

    private static string? ReadString(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty(key, out var p) &&
        p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
