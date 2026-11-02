using System.Windows;
using System.Collections.ObjectModel;
using System.Text;

namespace ACLocalTrafficStudio;

public partial class MainWindow : System.Windows.Window
{
    private readonly ObservableCollection<CarEntry> _cars = new();
    private readonly ObservableCollection<TrackEntry> _tracks = new();

    public MainWindow()
    {
        InitializeComponent();
        CarsGrid.ItemsSource = _cars;
        TracksGrid.ItemsSource = _tracks;

        var detected = AcScanner.FindAcRoot();
        if (detected is not null) RootBox.Text = detected;
        ReportBox.Text = "AC Local Traffic Studio 第一階段\\r\\n\\r\\n此版本只進行本地唯讀掃描。\\r\\n不會下載模組、不會修改 AC、CM 或 CSP 設定。";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "選擇 Assetto Corsa 根目錄",
            UseDescriptionForTitle = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            RootBox.Text = dialog.SelectedPath;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e)
    {
        var root = RootBox.Text.Trim().Trim('"');
        if (!AcScanner.IsAcRoot(root))
        {
            System.Windows.MessageBox.Show(
                "找不到 content/cars 或 content/tracks。請選擇 Assetto Corsa 根目錄，例如：\\nSteam\\\\steamapps\\\\common\\\\assettocorsa",
                "目錄不正確", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ScanButton.IsEnabled = false;
        BrowseButton.IsEnabled = false;
        StatusText.Text = "正在掃描本地車輛及地圖…";

        try
        {
            var result = await Task.Run(() => (
                Cars: AcScanner.ScanCars(root),
                Tracks: AcScanner.ScanTracks(root)
            ));

            _cars.Clear();
            foreach (var car in result.Cars) _cars.Add(car);
            _tracks.Clear();
            foreach (var track in result.Tracks) _tracks.Add(track);

            var report = new StringBuilder();
            report.AppendLine("AC LOCAL TRAFFIC STUDIO — 掃描報告");
            report.AppendLine("==================================");
            report.AppendLine($"遊戲目錄：{root}");
            report.AppendLine($"掃描時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine($"車輛資料夾：{_cars.Count}");
            report.AppendLine($"地圖/Layout：{_tracks.Count}");
            report.AppendLine();
            report.AppendLine("重要說明：");
            report.AppendLine("1. 本次只確認檔案存在與基本結構，不代表車輛已通過 AI 駕駛測試。");
            report.AppendLine("2. fast_lane.ai / fast_lane.aip 是初步線索；不同地圖/Layout 的 AI 支援仍需實測。");
            report.AppendLine("3. 沒有獨立 ai 資料夾，不一定代表 AC 原生 AI 無法使用該車。");
            report.AppendLine("4. 本程式不會修改、刪除或下載任何遊戲檔案。");
            report.AppendLine();
            report.AppendLine("車輛初檢摘要：");
            report.AppendLine($"基本物理資料存在：{_cars.Count(c => c.Status.StartsWith("基本檔案存在"))}");
            report.AppendLine($"待檢查資料：{_cars.Count(c => c.Status.StartsWith("資料不完整"))}");
            report.AppendLine();
            report.AppendLine("地圖初檢摘要：");
            report.AppendLine($"找到 fast_lane：{_tracks.Count(t => t.AiStatus.StartsWith("找到"))}");
            report.AppendLine($"未找到 fast_lane：{_tracks.Count(t => t.AiStatus.StartsWith("未找到"))}");

            ReportBox.Text = report.ToString();
            StatusText.Text = $"掃描完成：{_cars.Count} 部車輛資料夾、{_tracks.Count} 個地圖/Layout。";
        }
        catch (Exception ex)
        {
            StatusText.Text = "掃描失敗。";
            System.Windows.MessageBox.Show(ex.Message, "掃描錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ScanButton.IsEnabled = true;
            BrowseButton.IsEnabled = true;
        }
    }
}
