using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ACLocalTrafficStudio;

/// <summary>
/// 交通計畫匯出器。只會把唯讀的 JSON / Markdown 寫入本程式自己的匯出資料夾
/// （預設 %AppData%\ACLocalTrafficStudio\exports，或使用者指定的資料夾），
/// 永遠不會寫入 Assetto Corsa 的遊戲資料夾。
/// </summary>
public static class TrafficPackageExporter
{
    public static string DefaultExportDir => StudioPaths.ExportsDir;

    public static string ExportPlanJson(TrafficPlan plan, string? targetDir = null)
    {
        targetDir ??= DefaultExportDir;
        Directory.CreateDirectory(targetDir);
        var file = Path.Combine(targetDir, $"traffic_plan_{plan.GeneratedAt:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(file, JsonSerializer.Serialize(plan, StudioJson.Options));
        return file;
    }

    public static string ExportPlanMarkdown(TrafficPlan plan, string? targetDir = null)
    {
        targetDir ??= DefaultExportDir;
        Directory.CreateDirectory(targetDir);
        var file = Path.Combine(targetDir, $"traffic_plan_{plan.GeneratedAt:yyyyMMdd_HHmmss}.md");
        File.WriteAllText(file, BuildMarkdown(plan));
        return file;
    }

    public static string ExportProfileJson(TrafficProfile profile, string? targetDir = null)
    {
        targetDir ??= DefaultExportDir;
        Directory.CreateDirectory(targetDir);
        var file = Path.Combine(targetDir, $"profile_{profile.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(file, JsonSerializer.Serialize(profile, StudioJson.Options));
        return file;
    }

    public static string BuildMarkdown(TrafficPlan plan)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 交通計畫摘要 — {plan.ProfileName}");
        sb.AppendLine();
        sb.AppendLine($"- 地圖：{plan.TrackDisplayName}（{plan.MapKind}）");
        sb.AppendLine($"- 軌道長度：{plan.TrackLengthKm:F1} km");
        sb.AppendLine($"- 密度：{plan.Density:F0}%");
        sb.AppendLine($"- AI 車輛：{plan.VehicleCount} 部（每公里 {plan.EffectiveDensityPerKm:F2} 部）");
        sb.AppendLine($"- 車道數：{plan.Lanes}");
        sb.AppendLine($"- 產生時間：{plan.GeneratedAt:yyyy-MM-dd HH:mm:ss}（完全離線）");
        sb.AppendLine();
        if (plan.Warnings.Count > 0)
        {
            sb.AppendLine("## 注意事項");
            foreach (var w in plan.Warnings) sb.AppendLine($"- {w}");
            sb.AppendLine();
        }
        sb.AppendLine("## 車輛清單");
        sb.AppendLine();
        sb.AppendLine("| # | 車輛 | 品牌 | 生成位置 m | 車道 | 速度係數 | 侵略性 | 超車傾向 |");
        sb.AppendLine("|---|------|------|-----------:|-----:|---------:|---------:|---------:|");
        foreach (var v in plan.Vehicles)
        {
            sb.AppendLine($"| {v.Index} | {v.DisplayName} | {v.Brand} | {v.SpawnDistanceM:F0} | {v.Lane} | {v.SpeedMultiplier:F2} | {v.Behavior.Aggression:F2} | {v.Behavior.OvertakeBias:F2} |");
        }
        sb.AppendLine();
        sb.AppendLine("> 本檔案由 AC Local Traffic Studio 以唯讀方式產生，未修改任何遊戲檔案。");
        return sb.ToString();
    }
}
