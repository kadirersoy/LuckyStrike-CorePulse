using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using CorePulse.Core.Models;
using CorePulse.Core.Services;

namespace CorePulse.App.ViewModels;

public class SensorCategoryGroup
{
    public string CategoryName { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public ObservableCollection<SensorItem> Sensors { get; set; } = new();
}

public class DiscoveryViewModel : INotifyPropertyChanged
{
    private readonly HardwareSensorService _sensorService;
    private readonly ConfigService _configService;
    private string _searchText = string.Empty;
    private bool _isScanning;
    private int _totalFound;
    private int _selectedCount;

    public ObservableCollection<SensorCategoryGroup> CategoryGroups { get; } = new();

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetField(ref _searchText, value))
            {
                FilterSensors();
            }
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetField(ref _isScanning, value);
    }

    public int TotalFound
    {
        get => _totalFound;
        set => SetField(ref _totalFound, value);
    }

    public int SelectedCount
    {
        get => _selectedCount;
        set => SetField(ref _selectedCount, value);
    }

    public ICommand RescanCommand { get; }
    public ICommand SelectRecommendedCommand { get; }
    public ICommand SelectAllCommand { get; }
    public ICommand ClearAllCommand { get; }
    public ICommand SaveAndApplyCommand { get; }

    public event Action? RequestClose;

    public DiscoveryViewModel(HardwareSensorService sensorService, ConfigService configService)
    {
        _sensorService = sensorService;
        _configService = configService;

        RescanCommand = new RelayCommand(Rescan);
        SelectRecommendedCommand = new RelayCommand(SelectRecommended);
        SelectAllCommand = new RelayCommand(SelectAll);
        ClearAllCommand = new RelayCommand(ClearAll);
        SaveAndApplyCommand = new RelayCommand(SaveAndApply);

        LoadDiscoveredSensors();
    }

    public void LoadDiscoveredSensors()
    {
        CategoryGroups.Clear();

        var sensors = _sensorService.DiscoveredSensors;
        TotalFound = sensors.Count;
        SelectedCount = sensors.Count(s => s.IsSelected);

        var groups = sensors
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Key);

        foreach (var g in groups)
        {
            var catName = GetCategoryFriendlyName(g.Key);
            var subtitle = $"{g.Count()} Sensör Bulundu";

            var catGroup = new SensorCategoryGroup
            {
                CategoryName = catName,
                Subtitle = subtitle
            };

            foreach (var s in g)
            {
                s.PropertyChanged += (o, e) =>
                {
                    if (e.PropertyName == nameof(SensorItem.IsSelected))
                    {
                        SelectedCount = _sensorService.DiscoveredSensors.Count(x => x.IsSelected);
                    }
                };
                catGroup.Sensors.Add(s);
            }

            CategoryGroups.Add(catGroup);
        }
    }

    private void Rescan()
    {
        IsScanning = true;
        Task.Run(() =>
        {
            // Non-destructive rescan preserving aliases, widgets, and thresholds
            _sensorService.DiscoverSensors(preserveExistingSettings: true);
            Thread.Sleep(800); // Visual feedback
        }).ContinueWith(_ =>
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                LoadDiscoveredSensors();
                IsScanning = false;
            });
        });
    }

    private void SelectRecommended()
    {
        foreach (var s in _sensorService.DiscoveredSensors)
        {
            s.IsSelected = s.IsDefaultRecommended;
        }
        SelectedCount = _sensorService.DiscoveredSensors.Count(s => s.IsSelected);
    }

    private void SelectAll()
    {
        foreach (var s in _sensorService.DiscoveredSensors)
        {
            s.IsSelected = true;
        }
        SelectedCount = _sensorService.DiscoveredSensors.Count;
    }

    private void ClearAll()
    {
        foreach (var s in _sensorService.DiscoveredSensors)
        {
            s.IsSelected = false;
        }
        SelectedCount = 0;
    }

    private void SaveAndApply()
    {
        var settings = _configService.LoadSettings();
        settings.IsFirstRun = false;
        settings.SensorConfigs = _sensorService.DiscoveredSensors.Select(s => new SensorConfigEntry
        {
            SensorId = s.Id,
            CustomName = s.CustomName,
            WidgetType = s.WidgetType,
            IsSelected = s.IsSelected,
            WarningThreshold = s.WarningThreshold,
            CriticalThreshold = s.CriticalThreshold,
            IsAudioAlertEnabled = s.IsAudioAlertEnabled,
            SortOrder = s.SortOrder
        }).ToList();

        _configService.SaveSettings(settings);
        RequestClose?.Invoke();
    }

    private void FilterSensors()
    {
        // Simple search filtering
        LoadDiscoveredSensors();
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var q = _searchText.ToLowerInvariant();
            for (int i = CategoryGroups.Count - 1; i >= 0; i--)
            {
                var grp = CategoryGroups[i];
                var filtered = grp.Sensors.Where(s =>
                    s.OriginalName.ToLowerInvariant().Contains(q) ||
                    s.CustomName.ToLowerInvariant().Contains(q) ||
                    s.HardwareName.ToLowerInvariant().Contains(q)).ToList();

                grp.Sensors.Clear();
                foreach (var s in filtered) grp.Sensors.Add(s);

                if (grp.Sensors.Count == 0)
                {
                    CategoryGroups.RemoveAt(i);
                }
            }
        }
    }

    private static string GetCategoryFriendlyName(HardwareCategory cat) => cat switch
    {
        HardwareCategory.Cpu => "İşlemci (CPU)",
        HardwareCategory.Gpu => "Ekran Kartı (GPU)",
        HardwareCategory.Motherboard => "Anakart & Fanlar (Super I/O)",
        HardwareCategory.Memory => "RAM & Bellek",
        HardwareCategory.Storage => "Depolama (NVMe / SSD)",
        HardwareCategory.Network => "Ağ Bağdaştırıcıları",
        HardwareCategory.Controller => "Donanım Denetleyicileri",
        _ => "Diğer Donanımlar"
    };

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
