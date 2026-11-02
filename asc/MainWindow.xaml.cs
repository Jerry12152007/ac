using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ACLocalTrafficStudio;

public partial class MainWindow : System.Windows.Window
{
    private readonly ObservableCollection<CarEntry> _cars = new();
    private readonly ObservableCollection<TrackEntry> _tracks = new();
    private readonly ObservableCollection<ClassOverrideRow> _classRows = new();

    private List<TrafficProfile> _profiles = new();
    private StudioSettings _settings = new();
    private DensityController _density = new();
    private DispatcherTimer? _simTimer;
    private double _simElapsed;
    private List<DensitySchedulePoint> _schedule = new();
    private int _scheduleIndex;
    private TrafficPlan? _plan;
    private EnvironmentInfo? _env;

    public MainWindow()
    {
        InitializeComponent();
        CarsGrid.ItemsSource = _cars;
        TracksGrid.ItemsSource = _tracks;
        ClassGrid.ItemsSource = _classRows;

        _settings = ProfileStore.LoadSettings();
        _profiles = ProfileStore.LoadProfiles();
        _density = new DensityController(_settings.TargetDensity)
        {
            RampPerSecond = _settings.RampPerSecond,
            Immediate = _settings.ImmediateDensity
        };

        RefreshProfileLists();
        RefreshTrackCombo();

        DensitySlider.Value = _settings.TargetDensity;
        RampSlider.Value = _settings.RampPerSecond;
        DensityModeCombo.SelectedIndex = _settings.ImmediateDensity ? 0 : 1;
        ScheduleCombo.SelectedIndex = 0;
        PlanDensitySlider.Value = _settings.TargetDensity;
        LoadGlobalSliders();

        var detected = AcScanner.FindAcRoot();
        if (detected is not null) RootBox.Text = detected;

        EnvBox.Text = "尚未掃描。請確認 Assetto Corsa 根目錄後按「開始掃描」。";
        ReportBox.Text = "AC Local Traffic Studio V2.0（Phase 2）\n\n" +
                         "本程式完全離線運行、只讀取本機檔案，不會下載模組，也不會修改 AC、Content Manager 或 CSP 的任何檔案。\n\n" +
                         "請按「開始掃描」建立環境與資源清單。";
        UpdateDensityUi();
    }

    private TrafficProfile? ActiveProfile => ProfilesList.SelectedItem as TrafficProfile;

    // ------------------------------------------------------------------
    // 掃描
    // ------------------------------------------------------------------

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
        Offline.EnsureOffline();
        var root = RootBox.Text.Trim().Trim('"');
        if (!AcScanner.IsAcRoot(root))
        {
            System.Windows.MessageBox.Show(
                "找不到 content/cars 或 content/tracks。請選擇 Assetto Corsa 根目錄，例如：\nSteam\\steamapps\\common\\assettocorsa",
                "目錄不正確", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ScanButton.IsEnabled = false;
        BrowseButton.IsEnabled = false;
        StatusText.Text = "正在掃描本地環境、車輛及地圖…";

        try
        {
            var result = await Task.Run(() => (
                Cars: AcScanner.ScanCars(root),
                Tracks: AcScanner.ScanTracks(root),
                Env: AcScanner.ScanEnvironment(root)
            ));

            _cars.Clear();
            foreach (var car in result.Cars) _cars.Add(car);

            _tracks.Clear();
            foreach (var track in result.Tracks)
            {
                if (_settings.TrackLengthsKm.TryGetValue(track.Key, out var len) && len > 0)
                    track.LengthKm = len;
                _tracks.Add(track);
            }

            _env = result.Env;
            _env.CarCount = _cars.Count;
            _env.TrackCount = _tracks.Count;
            _env.SuitableCarCount = _cars.Count(c => c.Suitability is TrafficSuitability.Good or TrafficSuitability.Possible);
            _env.HasShutoko = _tracks.Any(t => t.Kind == MapKind.HighwayShutoko);
            _env.HasNordschleife = _tracks.Any(t => t.Kind == MapKind.TrackDayNordschleife);

            RefreshTrackCombo();
            RefreshClassRows();
            RefreshEnvOverview();
            BuildReport(root);
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

    private void RefreshTrackCombo()
    {
        PlanTrackCombo.ItemsSource = null;
        PlanTrackCombo.ItemsSource = _tracks;
    }

    private void RefreshEnvOverview()
    {
        var e = _env;
        ShutokoBadge.Visibility = _tracks.Any(t => t.Kind == MapKind.HighwayShutoko) ? Visibility.Visible : Visibility.Collapsed;
        NordschleifeBadge.Visibility = _tracks.Any(t => t.Kind == MapKind.TrackDayNordschleife) ? Visibility.Visible : Visibility.Collapsed;
        CspBadge.Visibility = e?.HasCustomShadersPatch == true ? Visibility.Visible : Visibility.Collapsed;
        CmBadge.Visibility = e?.HasContentManager == true ? Visibility.Visible : Visibility.Collapsed;

        var sb = new StringBuilder();
        sb.AppendLine("環境總覽（完全離線、唯讀）");
        sb.AppendLine();
        if (e != null)
        {
            sb.AppendLine($"AC 根目錄：{e.AcRoot}");
            sb.AppendLine($"AC 版本：{(string.IsNullOrWhiteSpace(e.AcVersion) ? "未知" : e.AcVersion)}");
            sb.AppendLine($"Custom Shaders Patch：{(e.HasCustomShadersPatch ? "已偵測（dxgi.dll / extension）" : "未偵測")}");
            sb.AppendLine($"Content Manager：{(e.HasContentManager ? "已偵測" : "未偵測（非必要）")}");
            sb.AppendLine($"Documents\\Assetto Corsa：{(e.DocumentsFolderExists ? e.DocumentsFolder : "未找到")}");
            foreach (var n in e.Notes) sb.AppendLine($"備註：{n}");
            sb.AppendLine();
        }
        sb.AppendLine($"車輛：{_cars.Count} 部（初步適合交通 {_cars.Count(c => c.Suitability is TrafficSuitability.Good or TrafficSuitability.Possible)} 部）");
        sb.AppendLine($"地圖/Layout：{_tracks.Count} 個");
        sb.AppendLine($"🛣️ Shutoko 高速公路地圖：{_tracks.Count(t => t.Kind == MapKind.HighwayShutoko)} 個");
        sb.AppendLine($"🏁 Nordschleife 賽道日地圖：{_tracks.Count(t => t.Kind == MapKind.TrackDayNordschleife)} 個");
        sb.AppendLine();
        sb.AppendLine("下一步：在「交通預設檔」選擇或編輯預設檔 →「AI 行為管理」調整行為 →「密度控制」設定密度 →「交通計畫」產生並匯出計畫。");
        EnvBox.Text = sb.ToString();
    }

    private void BuildReport(string root)
    {
        var sb = new StringBuilder();
        sb.AppendLine("AC LOCAL TRAFFIC STUDIO — 掃描報告（唯讀）");
        sb.AppendLine("========================================");
        sb.AppendLine($"遊戲目錄：{root}");
        sb.AppendLine($"掃描時間：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (_env != null)
        {
            sb.AppendLine($"AC 版本：{(string.IsNullOrWhiteSpace(_env.AcVersion) ? "未知" : _env.AcVersion)}");
            sb.AppendLine($"Custom Shaders Patch：{(_env.HasCustomShadersPatch ? "已偵測" : "未偵測")}");
            sb.AppendLine($"Content Manager：{(_env.HasContentManager ? "已偵測" : "未偵測")}");
            sb.AppendLine($"Documents 資料夾：{(_env.DocumentsFolderExists ? _env.DocumentsFolder : "未找到")}");
        }
        sb.AppendLine($"車輛資料夾：{_cars.Count}（初步適合交通：{_cars.Count(c => c.Suitability is TrafficSuitability.Good or TrafficSuitability.Possible)}）");
        sb.AppendLine($"地圖/Layout：{_tracks.Count}（Shutoko：{_tracks.Count(t => t.Kind == MapKind.HighwayShutoko)}；Nordschleife：{_tracks.Count(t => t.Kind == MapKind.TrackDayNordschleife)}）");
        sb.AppendLine();
        sb.AppendLine("車輛合適度摘要：");
        foreach (var g in _cars.GroupBy(c => c.Suitability).OrderByDescending(g => g.Count()))
            sb.AppendLine($"  {g.Key}：{g.Count()} 部");
        sb.AppendLine();
        sb.AppendLine("地圖類型摘要：");
        foreach (var g in _tracks.GroupBy(t => t.Kind).OrderByDescending(g => g.Count()))
            sb.AppendLine($"  {g.Key}：{g.Count()} 個");
        sb.AppendLine();
        sb.AppendLine("重要說明：");
        sb.AppendLine("1. 掃描只讀取本機檔案，不會修改、刪除或下載任何檔案。");
        sb.AppendLine("2. 「適合交通」為依 ui_car.json 標籤的初步分類，實際 AI 表現仍需實測。");
        sb.AppendLine("3. 本程式完全離線運行，不會連線到任何外部伺服器。");
        sb.AppendLine("4. 交通計畫匯出為唯讀 JSON / MD，不會寫入遊戲資料夾。");
        ReportBox.Text = sb.ToString();
    }

    // ------------------------------------------------------------------
    // 本地車輛 + 個別車輛行為覆寫
    // ------------------------------------------------------------------

    private void CarsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        var car = CarsGrid.SelectedItem as CarEntry;
        if (car == null)
        {
            CarDetailLabel.Text = "尚未選取車輛";
            return;
        }
        CarDetailLabel.Text = $"{car.DisplayName}（{car.Folder}）— 合適度：{car.SuitabilityText}";
        var p = ActiveProfile;
        var ov = p != null && p.CarOverrides.TryGetValue(car.Folder, out var o) ? o : null;
        CarOverrideCheck.IsChecked = ov != null && !ov.IsEmpty;
        CarAggrSlider.Value = ov?.Aggression ?? p?.BaseBehavior.Aggression ?? 0.5;
        CarSpeedSlider.Value = ov?.SpeedMultiplier ?? p?.BaseBehavior.SpeedMultiplier ?? 1.0;
        CarLaneSlider.Value = ov?.LaneChangeFrequency ?? p?.BaseBehavior.LaneChangeFrequency ?? 0.5;
    }

    private void ApplyCarOverride_Click(object sender, RoutedEventArgs e)
    {
        var car = CarsGrid.SelectedItem as CarEntry;
        var p = ActiveProfile;
        if (car == null || p == null)
        {
            StatusText.Text = "請先掃描、選取車輛，並選擇作用中的預設檔。";
            return;
        }
        if (CarOverrideCheck.IsChecked != true)
        {
            StatusText.Text = "請先勾選「啟用此車輛專屬行為覆寫」。";
            return;
        }
        p.CarOverrides[car.Folder] = new BehaviorOverride
        {
            Aggression = CarAggrSlider.Value,
            SpeedMultiplier = CarSpeedSlider.Value,
            LaneChangeFrequency = CarLaneSlider.Value
        };
        ProfileStore.SaveProfiles(_profiles);
        StatusText.Text = $"已儲存車輛覆寫：{car.DisplayName}（預設檔：{p.Name}）";
    }

    private void ClearCarOverride_Click(object sender, RoutedEventArgs e)
    {
        var car = CarsGrid.SelectedItem as CarEntry;
        var p = ActiveProfile;
        if (car == null || p == null) return;
        if (p.CarOverrides.Remove(car.Folder))
        {
            ProfileStore.SaveProfiles(_profiles);
            StatusText.Text = $"已清除車輛覆寫：{car.DisplayName}";
        }
        CarOverrideCheck.IsChecked = false;
    }

    // ------------------------------------------------------------------
    // 地圖與 Layout
    // ------------------------------------------------------------------

    private void TracksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        var t = TracksGrid.SelectedItem as TrackEntry;
        if (t == null)
        {
            TrackDetailLabel.Text = "尚未選取地圖";
            return;
        }
        TrackDetailLabel.Text = $"{t.DisplayName} — {t.KindText}";
        TrackLengthBox.Text = t.LengthKm > 0 ? t.LengthKm.ToString("F1", CultureInfo.InvariantCulture) : "0";
        TrackProfileHint.Text = $"建議預設檔：{ProfileNameFor(MapClassifier.SuggestedProfileId(t.Kind))}";
    }

    private void SaveTrackLength_Click(object sender, RoutedEventArgs e)
    {
        var t = TracksGrid.SelectedItem as TrackEntry;
        if (t == null)
        {
            StatusText.Text = "請先選取地圖/Layout。";
            return;
        }
        var raw = TrackLengthBox.Text.Trim().Replace(',', '.');
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var km) || km < 0)
        {
            StatusText.Text = "長度必須是大於等於 0 的數字（km）。";
            return;
        }
        t.LengthKm = km;
        if (km > 0) _settings.TrackLengthsKm[t.Key] = km;
        else _settings.TrackLengthsKm.Remove(t.Key);
        ProfileStore.SaveSettings(_settings);
        TracksGrid.Items.Refresh();
        StatusText.Text = $"已儲存 {t.DisplayName} 的軌道長度：{km:F1} km";
    }

    private string ProfileNameFor(string id) => _profiles.FirstOrDefault(p => p.Id == id)?.Name ?? id;

    // ------------------------------------------------------------------
    // 交通預設檔
    // ------------------------------------------------------------------

    private void RefreshProfileLists(string? selectId = null)
    {
        var id = selectId ?? _settings.ActiveProfileId;
        ProfilesList.ItemsSource = null;
        ProfilesList.ItemsSource = _profiles;
        PlanProfileCombo.ItemsSource = null;
        PlanProfileCombo.ItemsSource = _profiles;
        var p = _profiles.FirstOrDefault(x => x.Id == id) ?? _profiles.FirstOrDefault();
        if (p != null)
        {
            ProfilesList.SelectedItem = p;
            PlanProfileCombo.SelectedItem = p;
        }
    }

    private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        var p = ActiveProfile;
        ProfileEditor.IsEnabled = p != null;
        if (p == null) return;
        _settings.ActiveProfileId = p.Id;
        ProfileStore.SaveSettings(_settings);
        LoadProfileToEditor(p);
        RefreshClassRows();
        UpdateDensityUi();
    }

    private void LoadProfileToEditor(TrafficProfile p)
    {
        ProfileNameBox.Text = p.Name;
        ProfileKindCombo.SelectedIndex = p.Kind switch
        {
            ProfileKind.Highway => 0,
            ProfileKind.TrackDay => 1,
            _ => 2
        };
        ProfileDescBox.Text = p.Description;
        ProfileMaxCarsSlider.Value = p.MaxAiCars;
        ProfileLanesSlider.Value = p.Lanes;
        ProfileMinSpeedSlider.Value = p.MinSpeedMultiplier;
        ProfileMaxSpeedSlider.Value = p.MaxSpeedMultiplier;
        ProfileSpawnMinSlider.Value = p.SpawnAheadMinM;
        ProfileSpawnMaxSlider.Value = p.SpawnAheadMaxM;
        ProfileLengthSlider.Value = p.DefaultTrackLengthKm;
        AllowedTagsBox.Text = p.AllowedTags;
        ExcludedTagsBox.Text = p.ExcludedTags;
        ProfileAggrSlider.Value = p.BaseBehavior.Aggression;
        ProfileSpeedSlider.Value = p.BaseBehavior.SpeedMultiplier;
        ProfileLaneSlider.Value = p.BaseBehavior.LaneChangeFrequency;
        ProfileFollowSlider.Value = p.BaseBehavior.FollowDistanceFactor;
        ProfileOvertakeSlider.Value = p.BaseBehavior.OvertakeBias;
        ProfileRubberSlider.Value = p.BaseBehavior.RubberneckProbability;
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var p = ActiveProfile;
        if (p == null)
        {
            StatusText.Text = "請先選取要儲存的預設檔。";
            return;
        }
        var name = ProfileNameBox.Text.Trim();
        p.Name = name.Length > 0 ? name : "未命名預設檔";
        p.Kind = SelectedKind(ProfileKindCombo);
        p.Description = ProfileDescBox.Text;
        p.MaxAiCars = (int)Math.Round(ProfileMaxCarsSlider.Value);
        p.Lanes = Math.Max(1, (int)Math.Round(ProfileLanesSlider.Value));
        p.MinSpeedMultiplier = ProfileMinSpeedSlider.Value;
        p.MaxSpeedMultiplier = Math.Max(ProfileMaxSpeedSlider.Value, p.MinSpeedMultiplier);
        p.SpawnAheadMinM = ProfileSpawnMinSlider.Value;
        p.SpawnAheadMaxM = Math.Max(ProfileSpawnMaxSlider.Value, p.SpawnAheadMinM);
        p.DefaultTrackLengthKm = ProfileLengthSlider.Value;
        p.AllowedTags = AllowedTagsBox.Text;
        p.ExcludedTags = ExcludedTagsBox.Text;
        p.BaseBehavior = new AiBehaviorSettings
        {
            Aggression = ProfileAggrSlider.Value,
            SpeedMultiplier = ProfileSpeedSlider.Value,
            LaneChangeFrequency = ProfileLaneSlider.Value,
            FollowDistanceFactor = ProfileFollowSlider.Value,
            OvertakeBias = ProfileOvertakeSlider.Value,
            RubberneckProbability = ProfileRubberSlider.Value
        };
        AiBehaviorSettings.Clamp(p.BaseBehavior);
        ProfileStore.SaveProfiles(_profiles);
        RefreshProfileLists(p.Id);
        StatusText.Text = $"已儲存預設檔：{p.Name}";
    }

    private static ProfileKind SelectedKind(ComboBox combo) =>
        Enum.TryParse<ProfileKind>((combo.SelectedItem as ComboBoxItem)?.Tag?.ToString(), out var k)
            ? k
            : ProfileKind.Custom;

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var p = new TrafficProfile
        {
            Name = "新自訂預設檔",
            Kind = ProfileKind.Custom,
            Description = "由全域預設行為建立的自訂預設檔。",
            BaseBehavior = _settings.GlobalBehavior.Clone()
        };
        _profiles.Add(p);
        ProfileStore.SaveProfiles(_profiles);
        RefreshProfileLists(p.Id);
        StatusText.Text = "已新增自訂預設檔，編輯後按「儲存預設檔」。";
    }

    private void DeleteProfile_Click(object sender, RoutedEventArgs e)
    {
        var p = ActiveProfile;
        if (p == null) return;
        if (p.IsBuiltIn)
        {
            System.Windows.MessageBox.Show("內建預設檔不可刪除；如需還原請按「重設內建」。", "無法刪除",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _profiles.Remove(p);
        ProfileStore.SaveProfiles(_profiles);
        RefreshProfileLists();
        StatusText.Text = $"已刪除預設檔：{p.Name}";
    }

    private void ResetBuiltIns_Click(object sender, RoutedEventArgs e)
    {
        var customs = _profiles.Where(x => !x.IsBuiltIn).ToList();
        _profiles = TrafficProfile.BuiltIns();
        _profiles.AddRange(customs);
        ProfileStore.SaveProfiles(_profiles);
        RefreshProfileLists(_settings.ActiveProfileId);
        StatusText.Text = "已重設內建預設檔（自訂預設檔保留）。";
    }

    private void ExportProfile_Click(object sender, RoutedEventArgs e)
    {
        var p = ActiveProfile;
        if (p == null)
        {
            StatusText.Text = "請先選取要匯出的預設檔。";
            return;
        }
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "預設檔 JSON|*.json",
            FileName = p.Id + ".profile.json",
            Title = "匯出交通預設檔（唯讀匯出，不寫入遊戲資料夾）"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            ProfileStore.SaveSingleProfileFile(p, dlg.FileName);
            StatusText.Text = $"已匯出預設檔：{dlg.FileName}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "匯出失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportProfile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "預設檔 JSON|*.json",
            Title = "匯入交通預設檔"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var p = ProfileStore.LoadSingleProfileFile(dlg.FileName);
            if (p == null) throw new InvalidDataException("無法解析預設檔 JSON。");
            if (_profiles.Any(x => x.Id == p.Id)) p.Id = Guid.NewGuid().ToString("N");
            p.IsBuiltIn = false;
            _profiles.Add(p);
            ProfileStore.SaveProfiles(_profiles);
            RefreshProfileLists(p.Id);
            StatusText.Text = $"已匯入預設檔：{p.Name}";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "匯入失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ------------------------------------------------------------------
    // AI 行為管理
    // ------------------------------------------------------------------

    private void LoadGlobalSliders()
    {
        var g = _settings.GlobalBehavior;
        GlobalAggrSlider.Value = g.Aggression;
        GlobalSpeedSlider.Value = g.SpeedMultiplier;
        GlobalLaneSlider.Value = g.LaneChangeFrequency;
        GlobalFollowSlider.Value = g.FollowDistanceFactor;
        GlobalOvertakeSlider.Value = g.OvertakeBias;
        GlobalRubberSlider.Value = g.RubberneckProbability;
    }

    private void SaveGlobal_Click(object sender, RoutedEventArgs e)
    {
        _settings.GlobalBehavior = new AiBehaviorSettings
        {
            Aggression = GlobalAggrSlider.Value,
            SpeedMultiplier = GlobalSpeedSlider.Value,
            LaneChangeFrequency = GlobalLaneSlider.Value,
            FollowDistanceFactor = GlobalFollowSlider.Value,
            OvertakeBias = GlobalOvertakeSlider.Value,
            RubberneckProbability = GlobalRubberSlider.Value
        };
        AiBehaviorSettings.Clamp(_settings.GlobalBehavior);
        ProfileStore.SaveSettings(_settings);
        StatusText.Text = "已儲存全域預設 AI 行為。";
    }

    private void RefreshClassRows()
    {
        _classRows.Clear();
        var p = ActiveProfile;
        var classes = _cars
            .Select(c => c.ClassName)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase);
        foreach (var cls in classes)
        {
            var ov = p != null && p.ClassOverrides.TryGetValue(cls, out var o) ? o : null;
            _classRows.Add(new ClassOverrideRow
            {
                ClassName = cls,
                Aggression = BehaviorOverride.F(ov?.Aggression),
                SpeedMultiplier = BehaviorOverride.F(ov?.SpeedMultiplier),
                LaneChangeFrequency = BehaviorOverride.F(ov?.LaneChangeFrequency),
                Source = ov?.Clone() ?? new BehaviorOverride()
            });
        }
    }

    private void ClassGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        var row = ClassGrid.SelectedItem as ClassOverrideRow;
        if (row == null) return;
        ClassNameBox.Text = row.ClassName;
        ClassAggrSlider.Value = row.Source.Aggression ?? 0.5;
        ClassSpeedSlider.Value = row.Source.SpeedMultiplier ?? 1.0;
        ClassLaneSlider.Value = row.Source.LaneChangeFrequency ?? 0.5;
    }

    private void SaveClassOverride_Click(object sender, RoutedEventArgs e)
    {
        var p = ActiveProfile;
        if (p == null)
        {
            StatusText.Text = "請先選擇作用中的預設檔。";
            return;
        }
        var key = ClassNameBox.Text.Trim();
        if (key.Length == 0)
        {
            StatusText.Text = "請輸入級別名稱（例如 street、suv）。";
            return;
        }
        p.ClassOverrides[key] = new BehaviorOverride
        {
            Aggression = ClassAggrSlider.Value,
            SpeedMultiplier = ClassSpeedSlider.Value,
            LaneChangeFrequency = ClassLaneSlider.Value
        };
        ProfileStore.SaveProfiles(_profiles);
        RefreshClassRows();
        StatusText.Text = $"已儲存級別覆寫：{key}（預設檔：{p.Name}）";
    }

    private void ClearClassOverride_Click(object sender, RoutedEventArgs e)
    {
        var p = ActiveProfile;
        var key = ClassNameBox.Text.Trim();
        if (p == null || key.Length == 0) return;
        if (p.ClassOverrides.Remove(key))
        {
            ProfileStore.SaveProfiles(_profiles);
            StatusText.Text = $"已清除級別覆寫：{key}";
        }
        RefreshClassRows();
    }

    // ------------------------------------------------------------------
    // 密度控制
    // ------------------------------------------------------------------

    private void DensitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        _density.SetTarget(DensitySlider.Value);
        _settings.TargetDensity = _density.TargetDensity;
        ProfileStore.SaveSettings(_settings);
        UpdateDensityUi();
    }

    private void RampSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsInitialized) return;
        _density.RampPerSecond = RampSlider.Value;
        _settings.RampPerSecond = RampSlider.Value;
        ProfileStore.SaveSettings(_settings);
    }

    private void DensityModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        _density.Immediate = DensityModeCombo.SelectedIndex == 0;
        _settings.ImmediateDensity = _density.Immediate;
        if (_density.Immediate) _density.SetTarget(_density.TargetDensity);
        ProfileStore.SaveSettings(_settings);
        UpdateDensityUi();
    }

    private void UpdateDensityUi()
    {
        var profile = PlanProfileCombo.SelectedItem as TrafficProfile ?? ActiveProfile;
        var max = profile?.MaxAiCars ?? 40;
        DensityStatusText.Text =
            $"目前密度：{_density.CurrentDensity:F0} / 100 ｜ 目標：{_density.TargetDensity:F0} ｜ AI 車數：{_density.CurrentCarCount(max)}（上限 {max}）";
        DensityBar.Value = _density.CurrentDensity;
        if (Math.Abs(PlanDensitySlider.Value - _density.TargetDensity) > 0.01)
            PlanDensitySlider.Value = _density.TargetDensity;
    }

    private void EnsureSimTimer()
    {
        if (_simTimer != null) return;
        _simTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _simTimer.Tick += SimTimer_Tick;
    }

    private void SimStart_Click(object sender, RoutedEventArgs e)
    {
        EnsureSimTimer();
        _simTimer!.Start();
        SimStartButton.IsEnabled = false;
        SimStopButton.IsEnabled = true;
        StatusText.Text = "密度模擬中…";
    }

    private void SimStop_Click(object sender, RoutedEventArgs e)
    {
        _simTimer?.Stop();
        SimStartButton.IsEnabled = true;
        SimStopButton.IsEnabled = false;
        StatusText.Text = "模擬已停止。";
    }

    private void SimTimer_Tick(object? sender, EventArgs e)
    {
        _simElapsed += 0.5;
        while (_scheduleIndex < _schedule.Count && _schedule[_scheduleIndex].AtSeconds <= _simElapsed)
        {
            _density.SetTarget(_schedule[_scheduleIndex].Density);
            _scheduleIndex++;
        }
        _density.Tick(0.5);
        UpdateDensityUi();
    }

    private void GenerateSchedule_Click(object sender, RoutedEventArgs e)
    {
        var duration = 1800.0;
        if (double.TryParse(ScheduleDurationBox.Text.Trim().Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
            duration = d;
        var key = (ScheduleCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "constant";
        _schedule = DensityController.BuildSchedule(key, duration, _density.TargetDensity);
        _scheduleIndex = 0;
        _simElapsed = 0;
        _settings.DensitySchedule = key;
        ProfileStore.SaveSettings(_settings);
        ScheduleBox.Text = string.Join(Environment.NewLine,
            _schedule.Select(p => $"t = {p.AtSeconds,8:F0} 秒 → 密度 {p.Density:F0}%"))
            + Environment.NewLine + "（按「套用排程到模擬」後，模擬會依排程調整目標密度）";
        StatusText.Text = $"已產生密度排程（{_schedule.Count} 個時間點）。";
    }

    private void ApplySchedule_Click(object sender, RoutedEventArgs e)
    {
        if (_schedule.Count == 0) GenerateSchedule_Click(sender, e);
        if (_simTimer == null || !_simTimer.IsEnabled) SimStart_Click(sender, e);
        StatusText.Text = "排程已套用；模擬會依時間調整目標密度。";
    }

    // ------------------------------------------------------------------
    // 交通計畫
    // ------------------------------------------------------------------

    private void PlanTrackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsInitialized) return;
        var t = PlanTrackCombo.SelectedItem as TrackEntry;
        if (t == null) return;
        var suggested = _profiles.FirstOrDefault(p => p.Id == MapClassifier.SuggestedProfileId(t.Kind));
        if (suggested != null) PlanProfileCombo.SelectedItem = suggested;
    }

    private void GeneratePlan_Click(object sender, RoutedEventArgs e)
    {
        var track = PlanTrackCombo.SelectedItem as TrackEntry;
        if (track == null)
        {
            StatusText.Text = "請先掃描並選擇地圖/Layout。";
            return;
        }
        var profile = PlanProfileCombo.SelectedItem as TrafficProfile ?? ActiveProfile ?? _profiles.FirstOrDefault();
        if (profile == null)
        {
            StatusText.Text = "請先選擇交通預設檔。";
            return;
        }
        var density = PlanDensitySlider.Value;
        _plan = TrafficPlanner.Build(profile, track, density, _cars.ToList(),
            RootBox.Text.Trim().Trim('"'), seed: Environment.TickCount);
        PlanGrid.ItemsSource = null;
        PlanGrid.ItemsSource = _plan.Vehicles;
        PlanSummary.Text = $"計畫：{profile.Name} × {track.DisplayName}（{track.KindText}）｜密度 {density:F0}%｜車輛 {_plan.VehicleCount} 部｜" +
                           $"每公里 {_plan.EffectiveDensityPerKm:F2} 部｜車道數 {_plan.Lanes}｜長度 {_plan.TrackLengthKm:F1} km";
        PlanWarnings.Text = _plan.Warnings.Count == 0
            ? ""
            : "注意：" + Environment.NewLine + string.Join(Environment.NewLine, _plan.Warnings);
        StatusText.Text = $"已產生交通計畫：{_plan.VehicleCount} 部 AI 車輛（預設檔：{profile.Name}）。";
    }

    private void ExportPlan_Click(object sender, RoutedEventArgs e)
    {
        if (_plan == null)
        {
            StatusText.Text = "請先在「交通計畫」產生計畫。";
            return;
        }
        try
        {
            var path = TrafficPackageExporter.ExportPlanJson(_plan);
            StatusText.Text = $"已匯出計畫 JSON：{path}";
            System.Windows.MessageBox.Show($"已匯出（唯讀，未修改任何遊戲檔案）：\n{path}",
                "匯出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "匯出失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportSummary_Click(object sender, RoutedEventArgs e)
    {
        if (_plan == null)
        {
            StatusText.Text = "請先在「交通計畫」產生計畫。";
            return;
        }
        try
        {
            var path = TrafficPackageExporter.ExportPlanMarkdown(_plan);
            StatusText.Text = $"已匯出計畫摘要 MD：{path}";
            System.Windows.MessageBox.Show($"已匯出（唯讀，未修改任何遊戲檔案）：\n{path}",
                "匯出完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(ex.Message, "匯出失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
