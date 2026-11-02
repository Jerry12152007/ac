namespace ACLocalTrafficStudio;

/// <summary>
/// 離線政策。本程式碼庫中不包含任何網路 API 呼叫（沒有 HttpClient / WebClient / Socket / WebRequest），
/// 所有資料來源都是本機檔案，所有輸出都寫入本程式自己的資料夾。
/// </summary>
public static class Offline
{
    public const string Policy =
        "本程式完全離線運行：不會連線到任何外部伺服器，不會下載或上傳任何資料，也不會修改任何遊戲檔案。";

    /// <summary>刻意保留的 no-op：作為「離線保證」的明確檢查點，提醒開發者不要引入網路程式碼。</summary>
    public static void EnsureOffline()
    {
        // Deliberately empty. This application must never use the network.
    }
}
