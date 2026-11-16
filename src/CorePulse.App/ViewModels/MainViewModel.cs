using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using CorePulse.Core.Models;
using CorePulse.Core.Services;

namespace CorePulse.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly HardwareSensorService _sensorService;
    private readonly ConfigService _configService;
    private readonly AlertService _alertService;

    private bool _isAlertSimulated;
    private bool _alertBannerVisible;
    private string _alertBannerText = string.Empty;
    private bool _isEditMode;
    private string _statusMessage = "Sistem Aktif - Tüm Donanımlar İzleniyor";
    private int _discoveredCount;
    private int _selectedCount;

    private SensorItem? _heroCpuClock;
    private SensorItem? _heroGpuClock;
    private SensorItem? _heroCpuTemp;
    private SensorItem? _heroGpuUsage;

    public ObservableCollection<SensorItem> AllSensors { get; } = new();
    public ObservableCollection<SensorItem> DashboardListSensors { get; } = new();

    public SensorItem? HeroCpuClock
    {
        get => _heroCpuClock;
        set => SetField(ref _heroCpuClock, value);
    }

    public SensorItem? HeroGpuClock
    {
        get => _heroGpuClock;
        set => SetField(ref _heroGpuClock, value);
    }

    public SensorItem? HeroCpuTemp
    {
        get => _heroCpuTemp;
        set => SetField(ref _heroCpuTemp, value);
    }

    public SensorItem? HeroGpuUsage
    {
        get => _heroGpuUsage;
        set => SetField(ref _heroGpuUsage, value);
    }

    public bool IsAlertSimulated
    {
        get => _isAlertSimulated;
        set => SetField(ref _isAlertSimulated, value);
    }

    public bool AlertBannerVisible
    {
        get => _alertBannerVisible;
        set => SetField(ref _alertBannerVisible, value);
    }

    public string AlertBannerText
    {
        get => _alertBannerText;
        set => SetField(ref _alertBannerText, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        set => SetField(ref _isEditMode, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public int DiscoveredCount
    {
        get => _discoveredCount;
        set => SetField(ref _discoveredCount, value);
    }

    public int SelectedCount
    {
        get => _selectedCount;
        set => SetField(ref _selectedCount, value);
    }

    public ICommand ToggleAlertSimulationCommand { get; }
    public ICommand CleanMemoryCommand { get; }
    public ICommand SilenceAlertCommand { get; }
    public ICommand ToggleEditModeCommand { get; }

    public event Action<double, double>? NewSparklineSample;

    public MainViewModel(HardwareSensorService sensorService, ConfigService configService, AlertService alertService)
    {
        _sensorService = sensorService;
        _configService = configService;
        _alertService = alertService;

        ToggleAlertSimulationCommand = new RelayCommand(ToggleAlertSimulation);
        CleanMemoryCommand = new RelayCommand(CleanMemory);
        SilenceAlertCommand = new RelayCommand(SilenceAlert);
        ToggleEditModeCommand = new RelayCommand(() => IsEditMode = !IsEditMode);

        _sensorService.ValuesUpdated += OnValuesUpdated;
        _sensorService.ThresholdExceeded += OnThresholdExceeded;
    }

    public void Initialize()
    {
        _sensorService.Initialize();
        RefreshSensorsCollections();
        _sensorService.StartPolling();
    }

    public void RefreshSensorsCollections()
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            var sensors = _sensorService.DiscoveredSensors;

            AllSensors.Clear();
            foreach (var s in sensors)
            {
                AllSensors.Add(s);
            }

            DiscoveredCount = AllSensors.Count;
            SelectedCount = AllSensors.Count(s => s.IsSelected);

            // Assign Hero Sensors
            HeroCpuClock = AllSensors.FirstOrDefault(s => s.Category == HardwareCategory.Cpu && s.Kind == SensorKind.Clock && s.IsSelected)
                        ?? AllSensors.FirstOrDefault(s => s.Category == HardwareCategory.Cpu && s.Kind == SensorKind.Clock);

            HeroGpuClock = AllSensors.FirstOrDefault(s => s.Category == HardwareCategory.Gpu && s.Kind == SensorKind.Clock && s.IsSelected)
                        ?? AllSensors.FirstOrDefault(s => s.Category == HardwareCategory.Gpu && s.Kind == SensorKind.Clock);

            HeroCpuTemp = AllSensors.FirstOrDefault(s => s.Category == HardwareCategory.Cpu && s.Kind == SensorKind.Temperature && s.IsSelected)
                       ?? AllSensors.FirstOrDefault(s => s.Category == HardwareCategory.Cpu && s.Kind == SensorKind.Temperature);

            HeroGpuUsage = AllSensors.FirstOrDefault(s => s.Category == HardwareCategory.Gpu && s.Kind == SensorKind.Load && s.IsSelected)
                        ?? AllSensors.FirstOrDefault(s => s.Category == HardwareCategory.Gpu && s.Kind == SensorKind.Load);

            // Populate Right-Side Telemetry List (Sensors marked as Selected that are KeyValue or CapacityBar, or not in hero)
            DashboardListSensors.Clear();
            var listItems = AllSensors
                .Where(s => s.IsSelected && s != HeroCpuClock && s != HeroGpuClock && s != HeroCpuTemp && s != HeroGpuUsage)
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Category);

            foreach (var s in listItems)
            {
                DashboardListSensors.Add(s);
            }
        });
    }

    private void OnValuesUpdated(IReadOnlyList<SensorItem> sensors)
    {
        Application.Current?.Dispatcher.Invoke(() =>
        {
            if (IsAlertSimulated) return; // Keep simulation override active if test running

            // Send sparkline data points (e.g. CPU Temp & GPU Usage)
            double cpuTemp = HeroCpuTemp?.Value ?? 50.0;
            double gpuUsage = HeroGpuUsage?.Value ?? 20.0;
            NewSparklineSample?.Invoke(cpuTemp, gpuUsage);

            // Check if any critical banner needs showing
            var criticalSensors = sensors.Where(s => s.IsSelected && s.IsCritical).ToList();
            if (criticalSensors.Count > 0)
            {
                var worst = criticalSensors.First();
                AlertBannerText = $"Kritik Eşik Uyarısı: {worst.DisplayName} = {worst.FormattedFull} (Eşik: {worst.CriticalThreshold}{worst.Unit})";
                AlertBannerVisible = true;
            }
            else if (!IsAlertSimulated)
            {
                AlertBannerVisible = false;
            }
        });
    }

    private void OnThresholdExceeded(SensorItem sensor, bool isCritical)
    {
        // Handled in alert service
    }

    private void ToggleAlertSimulation()
    {
        IsAlertSimulated = !IsAlertSimulated;

        if (IsAlertSimulated)
        {
            if (HeroCpuTemp != null)
            {
                HeroCpuTemp.Value = 89;
                HeroCpuTemp.IsCritical = true;
            }
            AlertBannerText = "Aşırı Isınma Testi: CPU Sıcaklığı 89°C (Kritik Eşik: 85°C Aşıldı!)";
            AlertBannerVisible = true;
            _alertService.TriggerAlert(HeroCpuTemp ?? new SensorItem { IsAudioAlertEnabled = true }, isCritical: true);
        }
        else
        {
            if (HeroCpuTemp != null)
            {
                HeroCpuTemp.Value = 52;
                HeroCpuTemp.IsCritical = false;
            }
            AlertBannerVisible = false;
            _alertService.ClearSnooze();
        }
    }

    private void CleanMemory()
    {
        long freed = MemoryOptimizer.OptimizeMemory();
        double freedMb = freed / (1024.0 * 1024.0);
        StatusMessage = freedMb > 0 
            ? $"RAM Önbelleği Temizlendi: ~{freedMb:F1} MB bellek boşaltıldı!"
            : "RAM Önbelleği optimize edildi ve standby listesi temizlendi.";

        MessageBox.Show(
            "Windows Bellek Önbelleği (Standby List) başarıyla temizlendi!\nOyun ve ağır uygulamalar için RAM serbest bırakıldı.",
            "Bellek Temizleyici",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void SilenceAlert()
    {
        _alertService.Snooze(TimeSpan.FromMinutes(5));
        AlertBannerVisible = false;
        StatusMessage = "Sesli alarm 5 dakika boyunca sessize alındı.";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
