using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace ACLocalTrafficStudio;

/// <summary>
/// 本地 JSON 持久化：預設檔與工作室設定都存放在本程式自己的資料夾
/// （預設 %AppData%\ACLocalTrafficStudio），完全離線，不觸碰遊戲資料夾。
/// 所有方法都接受可選的檔案路徑，方便單元測試。
/// </summary>
public static class ProfileStore
{
    public static string GetProfilesFilePath() => Path.Combine(StudioPaths.DataDir, "profiles.json");
    public static string GetSettingsFilePath() => Path.Combine(StudioPaths.DataDir, "settings.json");

    public static List<TrafficProfile> LoadProfiles(string? filePath = null)
    {
        filePath ??= GetProfilesFilePath();
        var builtIns = TrafficProfile.BuiltIns();
        if (!File.Exists(filePath)) return builtIns;
        try
        {
            var list = JsonSerializer.Deserialize<List<TrafficProfile>>(File.ReadAllText(filePath), StudioJson.Options)
                       ?? builtIns;
            // 確保內建預設檔一定存在（舊檔案遺失時自動補回）
            foreach (var b in builtIns)
                if (list.All(p => p.Id != b.Id))
                    list.Insert(0, b);
            return list;
        }
        catch
        {
            return builtIns;
        }
    }

    public static void SaveProfiles(IEnumerable<TrafficProfile> profiles, string? filePath = null)
    {
        filePath ??= GetProfilesFilePath();
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, JsonSerializer.Serialize(profiles.ToList(), StudioJson.Options));
    }

    public static StudioSettings LoadSettings(string? filePath = null)
    {
        filePath ??= GetSettingsFilePath();
        if (!File.Exists(filePath)) return new StudioSettings();
        try
        {
            return JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(filePath), StudioJson.Options)
                   ?? new StudioSettings();
        }
        catch
        {
            return new StudioSettings();
        }
    }

    public static void SaveSettings(StudioSettings settings, string? filePath = null)
    {
        filePath ??= GetSettingsFilePath();
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(filePath, JsonSerializer.Serialize(settings, StudioJson.Options));
    }

    /// <summary>匯出 / 匯入單一預設檔（與他人分享時使用）。</summary>
    public static TrafficProfile? LoadSingleProfileFile(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<TrafficProfile>(File.ReadAllText(path), StudioJson.Options);
        }
        catch
        {
            return null;
        }
    }

    public static void SaveSingleProfileFile(TrafficProfile profile, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(profile, StudioJson.Options));
    }
}
