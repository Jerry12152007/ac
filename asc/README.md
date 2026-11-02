# AC Local Traffic Studio V1.0 — Phase 1

繁體中文 Windows 桌面程式，第一階段只做 Assetto Corsa 本地環境掃描。

## 功能
- 自動尋找常見 Steam Assetto Corsa 安裝路徑
- 手動選擇 AC 根目錄
- 掃描 `content/cars`
- 讀取 `ui/ui_car.json` 的名稱與品牌
- 初步檢查 `data/` 或 `data.acd`
- 掃描 `content/tracks` 及 Layout
- 初步尋找 `ai/fast_lane.ai` 或 `ai/fast_lane.aip`
- 顯示掃描報告

## 安全範圍
此版本只讀取本機資料，不會修改、刪除、下載或覆蓋 AC、Content Manager、CSP 檔案。
「基本檔案存在」不等於 AI 相容性已通過；第二階段才會進行引擎與實際 AI 測試。

## 建置需求
- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022（可選，或使用 dotnet CLI）

## 建置及執行
在此資料夾開啟 PowerShell：

```powershell
dotnet restore
dotnet build -c Release
dotnet run
```

產生可執行檔：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  /p:PublishSingleFile=true `
  /p:IncludeNativeLibrariesForSelfExtract=true
```

輸出位於 `bin\Release\net8.0-windows\win-x64\publish\`。

## 使用
1. 啟動程式。
2. 確認 Assetto Corsa 根目錄；通常為：
   `C:\Program Files (x86)\Steam\steamapps\common\assettocorsa`
3. 如未自動找到，按「選擇資料夾」。
4. 按「開始掃描」。
5. 檢查「本地車輛」、「地圖與 Layout」及「掃描報告」。

## 已知限制
- 自動尋找路徑只涵蓋常見 Steam 位置；Steam 自訂 Library 請手動選擇。
- 第一版不會解析所有加密/封裝車輛資料。
- 不會判定所有車模能否由某個第三方 AI 引擎駕駛。
- 不會啟動遊戲或建立交通；這些屬於後續階段。
