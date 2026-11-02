# AC Local Traffic Studio V2.0 — 神力科莎本地 AI 交通管理工作室

繁體中文 Windows 桌面程式（WPF，.NET 8）。為《Assetto Corsa》打造一套**完全離線、唯讀**的本地 AI 交通管理工具。

> 🔌 **離線保證**：程式碼中不含任何網路 API 呼叫，不會連線到任何外部交通伺服器，也不會下載任何東西。
> 🛡️ **唯讀保證**：掃描只讀取本機檔案；匯出只寫入本程式自己的資料夾（`%AppData%\ACLocalTrafficStudio`），**永遠不會修改、刪除或覆蓋 AC / Content Manager / CSP 的檔案**。

## 功能總覽

| 分頁 | 功能 |
|------|------|
| 🖥️ 環境總覽 | 自動尋找 AC 根目錄（含 Steam `libraryfolders.vdf` 解析）、AC 版本、CSP / Content Manager 偵測、Shutoko / Nordschleife 地圖徽章 |
| 🚗 本地車輛 | 掃描 `content/cars`，讀取 `ui/ui_car.json` 名稱 / 品牌 / 級別 / 標籤，初步交通合適度分類，個別車輛 AI 行為覆寫 |
| 🗺️ 地圖與 Layout | 掃描 `content/tracks` 與各 Layout、`fast_lane` 初檢、**地圖類型分類**（🛣️ Shutoko 高速公路 / 🏁 Nordschleife 賽道日）、手動設定軌道長度 |
| 📋 交通預設檔 | 內建 **Shutoko 高速公路車流**、**Nordschleife 賽道日**、都市街道、深夜車流；可新增 / 編輯 / 刪除 / 匯入 / 匯出自訂預設檔（JSON） |
| 🤖 AI 行為管理 | 六軸行為模型（侵略性、速度係數、變道頻率、跟車距離、超車傾向、圍觀機率）；全域預設 + 級別（class）覆寫；解析優先順序：**個別車輛 ＞ 級別 ＞ 預設檔基礎 ＞ 全域** |
| 📊 密度控制 | 動態密度 0–100（AI 車數 = 最大車數 × 密度 ÷ 100）、立即 / 平滑過渡、密度排程（恒定 / 尖峰時段 / 深夜稀疏 / 賽道日漸進）、即時模擬 |
| 📦 交通計畫 | 依地圖 + 預設檔 + 密度產生完整車單（車輛、生成位置、車道、速度、行為），匯出唯讀 JSON + Markdown 摘要 |
| 📄 掃描報告 | 唯讀掃描報告與合適度 / 類型摘要 |

## 內建交通預設檔

- **🛣️ Shutoko 高速公路車流**：60 台上限、3 車道、速度 0.90–1.15、高變道頻率、中等侵略性（適合 Shutoko Revival Project）
- **🏁 Nordschleife 賽道日**：18 台上限、單車道、速度 0.75–1.00、高侵略性但保守車速（適合 Nordschleife / Touristenfahrten）
- **都市街道巡航**、**深夜稀疏車流**：一般地圖與夜晚情境

## 專案結構

```
asc/
  ACLocalTrafficStudio.sln
  ACLocalTrafficStudio.csproj        ← WPF 主程式（net8.0-windows）
  App.xaml / App.xaml.cs
  MainWindow.xaml / MainWindow.xaml.cs   ← 八個分頁的 UI 與事件
  Models.cs                          ← CarEntry / TrackEntry / 列舉
  AcScanner.cs                       ← AC 根目錄尋找、車輛 / 地圖 / 環境掃描（唯讀）
  Core/
    StudioPaths.cs                   ← 本地資料目錄（%AppData%\ACLocalTrafficStudio）
    Offline.cs                       ← 離線政策（刻意的 no-op 保證點）
    StudioJson.cs                    ← 統一 JSON 序列化選項
    MapClassifier.cs                 ← Shutoko / Nordschleife 地圖分類
    CarClassifier.cs                 ← 車輛交通合適度分類
    TrafficModels.cs                 ← TrafficProfile / AiBehaviorSettings / TrafficPlan …
    DensityController.cs             ← 動態密度控制器 + 排程
    TrafficPlanner.cs                ← 密度 → 具體車單的計畫產生器
    ProfileStore.cs                  ← 預設檔 / 設定 JSON 持久化（本地）
    TrafficPackageExporter.cs        ← 唯讀匯出 JSON / MD
  tests/ACLocalTrafficStudio.Tests/  ← xUnit 單元測試（net8.0-windows）
```

## 建置需求

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022（開啟 `asc/ACLocalTrafficStudio.sln`），或使用 dotnet CLI

## 建置、測試及執行

```powershell
cd asc
dotnet restore
dotnet build -c Release
dotnet test                       # 執行單元測試
dotnet run --project ACLocalTrafficStudio.csproj
```

發布單一執行檔：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

輸出位於 `bin\Release\net8.0-windows\win-x64\publish\`。

## 使用流程

1. 啟動程式，確認 Assetto Corsa 根目錄（通常 `C:\Program Files (x86)\Steam\steamapps\common\assettocorsa`；自訂 Steam Library 會自動解析 `libraryfolders.vdf`）。找不到時按「選擇資料夾」。
2. 按「開始掃描」→ 查看「環境總覽」（Shutoko / Nordschleife 徽章）、「本地車輛」、「地圖與 Layout」。
3. 在「地圖與 Layout」選取地圖，必要時設定實際軌道長度（km）。
4. 在「交通預設檔」選擇內建預設檔（Shutoko 地圖會自動建議高速公路車流；Nordschleife 會建議賽道日），或新增自訂。
5. 在「AI 行為管理」調整全域預設與級別覆寫；在「本地車輛」為個別車輛設定覆寫。
6. 在「密度控制」拖動密度滑桿（可開始模擬觀察平滑過渡，或套用密度排程）。
7. 在「交通計畫」選擇地圖 + 預設檔 + 密度，按「產生計畫」，檢視車單後匯出 JSON / MD。
8. 匯出檔案位於 `%AppData%\ACLocalTrafficStudio\exports\`，為唯讀產物，可自行複製到任何交通運行時模組使用。

## 本地資料位置

- 設定與預設檔：`%AppData%\ACLocalTrafficStudio\profiles.json`、`settings.json`
- 匯出：`%AppData%\ACLocalTrafficStudio\exports\`

## 交通計畫 JSON 結構（schemaVersion 2）

```jsonc
{
  "SchemaVersion": 2,
  "Offline": true,
  "ProfileName": "Shutoko 高速公路車流",
  "TrackDisplayName": "shutoko_revival_project",
  "MapKind": "HighwayShutoko",
  "TrackLengthKm": 32.0,
  "Density": 50,
  "VehicleCount": 30,
  "Vehicles": [
    { "Index": 1, "CarFolder": "...", "SpawnDistanceM": 512.3, "Lane": 1,
      "SpeedMultiplier": 1.02, "Behavior": { "Aggression": 0.45, "...": "..." } }
  ],
  "Warnings": []
}
```

## 安全範圍

- 只讀取本機檔案；不修改、刪除、下載或覆蓋 AC、Content Manager、CSP 檔案。
- 不啟動遊戲，不注入任何程式碼到遊戲程序；交通的實際運行由外部交通模組讀取本工具匯出的計畫檔。
- 「基本檔案存在」與「交通合適度」皆為初步啟發式判斷，不等於 AI 相容性已通過；實車 AI 測試屬於後續階段。

## 已知限制

- 地圖分類與車輛合適度為名稱 / 標籤啟發式判斷，特殊命名可能誤判（可手動調整長度與預設檔對應）。
- 不解析加密 / 封裝（data.acd 內容）的車輛資料。
- 第一版不判定所有車模能否由某個第三方 AI 引擎駕駛。

## 路線圖

- Phase 3：與遊戲內交通運行時（例如 CSP Lua / 外部模組）的橋接、實車 AI 行為測試報告、每車實測評分。
