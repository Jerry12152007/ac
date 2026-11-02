using System;
using System.IO;

namespace ACLocalTrafficStudio;

/// <summary>
/// 本程式所有本地資料的存放位置（完全離線，不寫入遊戲資料夾）。
/// </summary>
public static class StudioPaths
{
    private static string _dataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ACLocalTrafficStudio");

    /// <summary>程式資料目錄（預設為 %AppData%\ACLocalTrafficStudio）。可在測試時重新導向。</summary>
    public static string DataDir
    {
        get => _dataDir;
        set => _dataDir = string.IsNullOrWhiteSpace(value)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ACLocalTrafficStudio")
            : value;
    }

    public static string ProfilesDir => Path.Combine(DataDir, "profiles");
    public static string ExportsDir => Path.Combine(DataDir, "exports");
}
