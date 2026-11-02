using System;
using System.Collections.Generic;
using System.Linq;

namespace ACLocalTrafficStudio;

/// <summary>
/// 動態交通密度控制器（純邏輯，可在 UI 模擬中逐格推進，也可用於實際運行時）。
/// 密度為 0..100 的數值；實際 AI 車數 = MaxAiCars × 密度 / 100。
/// 支援「立即套用」與「平滑過渡」（依 RampPerSecond 漸進接近目標）。
/// </summary>
public sealed class DensityController
{
    public double TargetDensity { get; private set; } = 50;
    public double CurrentDensity { get; private set; } = 50;

    /// <summary>平滑模式下每秒變化的密度點數。</summary>
    public double RampPerSecond { get; set; } = 2.0;

    /// <summary>true = 立即套用目標密度。</summary>
    public bool Immediate { get; set; }

    public DensityController() { }

    public DensityController(double initialDensity)
    {
        TargetDensity = Clamp(initialDensity);
        CurrentDensity = TargetDensity;
    }

    public static double Clamp(double v) => Math.Clamp(v, 0, 100);

    public void SetTarget(double density)
    {
        TargetDensity = Clamp(density);
        if (Immediate) CurrentDensity = TargetDensity;
    }

    /// <summary>重設目前與目標密度（例如載入設定時）。</summary>
    public void Reset(double density)
    {
        TargetDensity = Clamp(density);
        CurrentDensity = TargetDensity;
    }

    /// <summary>推進模擬時間；平滑模式會讓目前密度朝目標移動。</summary>
    public void Tick(double deltaSeconds)
    {
        if (deltaSeconds <= 0) return;
        if (Immediate)
        {
            CurrentDensity = TargetDensity;
            return;
        }
        var diff = TargetDensity - CurrentDensity;
        var step = RampPerSecond * deltaSeconds;
        CurrentDensity = Math.Abs(diff) <= step ? TargetDensity : CurrentDensity + Math.Sign(diff) * step;
    }

    public bool IsAtTarget => Math.Abs(CurrentDensity - TargetDensity) < 0.001;

    public int TargetCarCount(int maxAiCars) => (int)Math.Round(maxAiCars * TargetDensity / 100.0);
    public int CurrentCarCount(int maxAiCars) => (int)Math.Round(maxAiCars * CurrentDensity / 100.0);

    /// <summary>
    /// 產生密度排程（動態調節預案）。
    /// schedule: constant / rush_hour / night / trackday_ramp。
    /// </summary>
    public static List<DensitySchedulePoint> BuildSchedule(string schedule, double durationSeconds, double constantLevel)
    {
        durationSeconds = Math.Clamp(durationSeconds, 10, 86400);
        List<(double frac, double density)> pts = schedule switch
        {
            "rush_hour" => new()
            {
                (0.00, 15), (0.12, 55), (0.30, 90), (0.55, 95), (0.75, 70), (0.90, 40), (1.00, 20)
            },
            "night" => new()
            {
                (0.0, 8), (0.4, 18), (0.8, 12), (1.0, 6)
            },
            "trackday_ramp" => new()
            {
                (0.0, 10), (0.2, 30), (0.5, 60), (0.8, 85), (1.0, 85)
            },
            _ => new() { (0.0, constantLevel), (1.0, constantLevel) }
        };
        return pts
            .Select(p => new DensitySchedulePoint
            {
                AtSeconds = Math.Round(p.frac * durationSeconds, 1),
                Density = Clamp(p.density)
            })
            .ToList();
    }
}
