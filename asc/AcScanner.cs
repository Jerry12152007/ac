using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ACLocalTrafficStudio;

/// <summary>
/// Assetto Corsa 本地環境掃描器。只讀取本機檔案，不修改、不下載任何東西。
/// </summary>
public static class AcScanner
{
    public static string? FindAcRoot()
    {
        var candidates = new List<string>();
        foreach (var drive in Environment.GetLogicalDrives())
        {
            candidates.Add(Path.Combine(drive, "Steam", "steamapps", "common", "assettocorsa"));
            candidates.Add(Path.Combine(drive, "Program Files (x86)", "Steam", "steamapps", "common", "assettocorsa"));
            candidates.Add(Path.Combine(drive, "Program Files", "Steam", "steamapps", "common", "assettocorsa"));
            candidates.Add(Path.Combine(drive, "Games", "SteamLibrary", "steamapps", "common", "assettocorsa"));
        }

        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        candidates.Add(Path.Combine(user, "Steam", "steamapps", "common", "assettocorsa"));

        // 解析 Steam libraryfolders.vdf，支援自訂 Library 位置
        foreach (var lib in ReadSteamLibraries())
            candidates.Add(Path.Combine(lib, "steamapps", "common", "assettocorsa"));

        return candidates.FirstOrDefault(IsAcRoot);
    }

    /// <summary>從各磁碟機常見的 Steam 安裝位置解析 libraryfolders.vdf，找出所有額外的 Steam Library。</summary>
    public static List<string> ReadSteamLibraries()
    {
        var result = new List<string>();
        try
        {
            var steamDirs = new List<string>();
            foreach (var drive in Environment.GetLogicalDrives())
            {
                steamDirs.Add(Path.Combine(drive, "Program Files (x86)", "Steam"));
                steamDirs.Add(Path.Combine(drive, "Program Files", "Steam"));
                steamDirs.Add(Path.Combine(drive, "Steam"));
            }
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            steamDirs.Add(Path.Combine(user, "Steam"));
            steamDirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steam"));

            foreach (var steamDir in steamDirs.Distinct())
            {
                var vdf = Path.Combine(steamDir, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) continue;
                try
                {
                    result.AddRange(ParseLibraryFolders(File.ReadAllText(vdf)));
                    if (Directory.Exists(steamDir)) result.Add(steamDir);
                }
                catch { /* 忽略單一 library 的解析錯誤 */ }
            }
        }
        catch { /* 完全離線，解析失敗時只回傳空清單 */ }
        return result.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<string> ParseLibraryFolders(string vdf)
    {
        var result = new List<string>();
        foreach (Match m in Regex.Matches(vdf, "\"path\"\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase))
        {
            var p = m.Groups[1].Value.Replace("\\\\", "\\");
            if (!string.IsNullOrWhiteSpace(p)) result.Add(p);
        }
        return result;
    }

    public static bool IsAcRoot(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
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
            var className = "";
            var tags = "";
            var details = new List<string>();

            if (File.Exists(uiPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(uiPath));
                    var e = doc.RootElement;
                    name = ReadString(e, "name") ?? name;
                    brand = ReadString(e, "brand") ?? "";
                    className = ReadString(e, "class") ?? "";
                    tags = ReadString(e, "tags") ?? "";
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

            var suitability = CarClassifier.Classify(className, tags, hasPhysics);

            result.Add(new CarEntry
            {
                Folder = folder,
                DisplayName = name,
                Brand = brand,
                ClassName = className,
                Tags = tags,
                FolderPath = dir,
                Status = hasPhysics ? "基本檔案存在・待 AI 實測" : "資料不完整・待檢查",
                Details = string.Join("；", details),
                HasPhysics = hasPhysics,
                HasAiFolder = hasAi,
                Suitability = suitability
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
        var kind = MapClassifier.Classify(track, layout);
        return new TrackEntry
        {
            Folder = track,
            Layout = layout,
            DisplayName = string.IsNullOrWhiteSpace(layout) ? track : $"{track} / {layout}",
            FolderPath = path,
            AiStatus = fastLane ? "找到 fast_lane 路線檔" : "未找到 fast_lane（需進一步檢查）",
            Kind = kind,
            HasFastLane = fastLane,
            LengthKm = 0
        };
    }

    /// <summary>掃描 AC 安裝環境本身（版本、CSP、Content Manager、Documents 資料夾）。唯讀。</summary>
    public static EnvironmentInfo ScanEnvironment(string acRoot)
    {
        var env = new EnvironmentInfo { AcRoot = acRoot };

        var exe = Path.Combine(acRoot, "assettocorsa.exe");
        if (File.Exists(exe))
        {
            try { env.AcVersion = FileVersionInfo.GetVersionInfo(exe).ProductVersion ?? ""; }
            catch { env.AcVersion = ""; }
        }

        env.HasCustomShadersPatch =
            File.Exists(Path.Combine(acRoot, "dxgi.dll")) ||
            Directory.Exists(Path.Combine(acRoot, "extension"));

        env.HasContentManager = File.Exists(Path.Combine(acRoot, "Content Manager.exe"));

        env.DocumentsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Documents", "Assetto Corsa");
        env.DocumentsFolderExists = Directory.Exists(env.DocumentsFolder);

        if (!env.HasCustomShadersPatch)
            env.Notes.Add("未偵測到 Custom Shaders Patch（dxgi.dll / extension 資料夾）。");
        if (!env.HasContentManager)
            env.Notes.Add("未偵測到 Content Manager.exe（非必要，僅供參考）。");
        if (!env.DocumentsFolderExists)
            env.Notes.Add("未找到 Documents\\Assetto Corsa 資料夾（尚未遊玩過或路徑不同）。");

        return env;
    }

    private static string? ReadString(JsonElement e, string key) =>
        e.ValueKind == JsonValueKind.Object &&
        e.TryGetProperty(key, out var p) &&
        p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}
