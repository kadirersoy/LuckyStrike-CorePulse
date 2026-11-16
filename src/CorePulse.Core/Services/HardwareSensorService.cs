using System.Timers;
using CorePulse.Core.Models;
using LibreHardwareMonitor.Hardware;
using Timer = System.Timers.Timer;

namespace CorePulse.Core.Services;

public class HardwareSensorService : ISensorService
{
    private readonly Computer _computer;
    private readonly ConfigService _configService;
    private readonly AlertService _alertService;
    private readonly Timer _pollingTimer;
    private readonly List<SensorItem> _discoveredSensors = new();
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isInitialized;

    public IReadOnlyList<SensorItem> DiscoveredSensors
    {
        get
        {
            lock (_lock)
            {
                return _discoveredSensors.ToList();
            }
        }
    }

    public bool IsRunning => _pollingTimer.Enabled;

    public event Action<IReadOnlyList<SensorItem>>? ValuesUpdated;
    public event Action<SensorItem, bool>? ThresholdExceeded;

    public HardwareSensorService(ConfigService configService, AlertService alertService)
    {
        _configService = configService;
        _alertService = alertService;

        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsNetworkEnabled = true,
            IsStorageEnabled = true,
            IsPsuEnabled = true
        };

        _pollingTimer = new Timer(1000);
        _pollingTimer.Elapsed += (s, e) => UpdateSensors();
    }

    public void Initialize()
    {
        if (_isInitialized) return;

        try
        {
            _computer.Open();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[HardwareSensorService] Computer.Open hatası: {ex.Message}");
        }

        DiscoverSensors(preserveExistingSettings: true);
        _isInitialized = true;
    }

    public void SetPollingInterval(int intervalMs)
    {
        _pollingTimer.Interval = Math.Max(250, intervalMs);
    }

    public void StartPolling()
    {
        _pollingTimer.Start();
    }

    public void StopPolling()
    {
        _pollingTimer.Stop();
    }

    public void DiscoverSensors(bool preserveExistingSettings = true)
    {
        lock (_lock)
        {
            var savedSettings = _configService.LoadSettings();
            var existingMap = _discoveredSensors.GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First());
            var savedConfigMap = savedSettings.SensorConfigs.GroupBy(c => c.SensorId).ToDictionary(g => g.Key, g => g.First());

            var foundSensors = new List<SensorItem>();

            try
            {
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    ScanHardwareRecursive(hardware, foundSensors);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HardwareSensorService] Donanım tarama hatası: {ex.Message}");
            }

            // Fallback mock sensors if running in an environment without hardware driver access
            if (foundSensors.Count == 0)
            {
                GenerateFallbackSensors(foundSensors);
            }

            // Merge with existing and saved user configs
            int sortOrder = 0;
            foreach (var sensor in foundSensors)
            {
                sensor.SortOrder = sortOrder++;

                // If user previously renamed or configured this sensor in saved settings
                if (savedConfigMap.TryGetValue(sensor.Id, out var savedConfig))
                {
                    sensor.CustomName = savedConfig.CustomName;
                    sensor.WidgetType = savedConfig.WidgetType;
                    sensor.IsSelected = savedConfig.IsSelected;
                    sensor.WarningThreshold = savedConfig.WarningThreshold;
                    sensor.CriticalThreshold = savedConfig.CriticalThreshold;
                    sensor.IsAudioAlertEnabled = savedConfig.IsAudioAlertEnabled;
                }
                else if (preserveExistingSettings && existingMap.TryGetValue(sensor.Id, out var existingSensor))
                {
                    sensor.CustomName = existingSensor.CustomName;
                    sensor.WidgetType = existingSensor.WidgetType;
                    sensor.IsSelected = existingSensor.IsSelected;
                    sensor.WarningThreshold = existingSensor.WarningThreshold;
                    sensor.CriticalThreshold = existingSensor.CriticalThreshold;
                    sensor.IsAudioAlertEnabled = existingSensor.IsAudioAlertEnabled;
                }
                else
                {
                    // Apply smart default classification for first discovery
                    ApplySmartDefaults(sensor);
                }
            }

            _discoveredSensors.Clear();
            _discoveredSensors.AddRange(foundSensors);
        }

        UpdateSensors();
    }

    private void ScanHardwareRecursive(IHardware hardware, List<SensorItem> list)
    {
        hardware.Update();

        var category = MapCategory(hardware.HardwareType);

        foreach (var subHardware in hardware.SubHardware)
        {
            ScanHardwareRecursive(subHardware, list);
        }

        foreach (var sensor in hardware.Sensors)
        {
            var sensorId = sensor.Identifier.ToString();
            if (list.Any(x => x.Id == sensorId)) continue;

            var item = new SensorItem
            {
                Id = sensorId,
                HardwareId = hardware.Identifier.ToString(),
                HardwareName = hardware.Name,
                Category = category,
                Kind = MapSensorKind(sensor.SensorType),
                OriginalName = sensor.Name,
                CustomName = sensor.Name,
                Unit = GetUnit(sensor.SensorType),
                Value = sensor.Value,
                Min = sensor.Min,
                Max = sensor.Max
            };

            list.Add(item);
        }
    }

    public void UpdateSensors()
    {
        lock (_lock)
        {
            try
            {
                foreach (var hardware in _computer.Hardware)
                {
                    hardware.Update();
                    UpdateHardwareSensors(hardware);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HardwareSensorService] Sensör güncelleme hatası: {ex.Message}");
            }

            // Check alerts on active selected sensors
            foreach (var sensor in _discoveredSensors)
            {
                if (sensor.IsSelected && sensor.IsCritical)
                {
                    _alertService.TriggerAlert(sensor, isCritical: true);
                    ThresholdExceeded?.Invoke(sensor, true);
                }
                else if (sensor.IsSelected && sensor.IsWarning)
                {
                    _alertService.TriggerAlert(sensor, isCritical: false);
                    ThresholdExceeded?.Invoke(sensor, false);
                }
            }

            ValuesUpdated?.Invoke(_discoveredSensors);
        }
    }

    private void UpdateHardwareSensors(IHardware hardware)
    {
        foreach (var subHardware in hardware.SubHardware)
        {
            subHardware.Update();
            UpdateHardwareSensors(subHardware);
        }

        foreach (var sensor in hardware.Sensors)
        {
            var match = _discoveredSensors.FirstOrDefault(s => s.Id == sensor.Identifier.ToString());
            if (match != null)
            {
                match.Value = sensor.Value;
                match.Min = sensor.Min;
                match.Max = sensor.Max;
            }
        }
    }

    private static HardwareCategory MapCategory(HardwareType type) => type switch
    {
        HardwareType.Cpu => HardwareCategory.Cpu,
        HardwareType.GpuNvidia => HardwareCategory.Gpu,
        HardwareType.GpuAmd => HardwareCategory.Gpu,
        HardwareType.GpuIntel => HardwareCategory.Gpu,
        HardwareType.Motherboard => HardwareCategory.Motherboard,
        HardwareType.SuperIO => HardwareCategory.Motherboard,
        HardwareType.Memory => HardwareCategory.Memory,
        HardwareType.Storage => HardwareCategory.Storage,
        HardwareType.Network => HardwareCategory.Network,
        HardwareType.Cooler => HardwareCategory.Controller,
        HardwareType.EmbeddedController => HardwareCategory.Controller,
        HardwareType.Psu => HardwareCategory.Controller,
        _ => HardwareCategory.Other
    };

    private static SensorKind MapSensorKind(SensorType type) => type switch
    {
        SensorType.Voltage => SensorKind.Voltage,
        SensorType.Clock => SensorKind.Clock,
        SensorType.Temperature => SensorKind.Temperature,
        SensorType.Load => SensorKind.Load,
        SensorType.Fan => SensorKind.Fan,
        SensorType.Flow => SensorKind.Flow,
        SensorType.Power => SensorKind.Power,
        SensorType.Data => SensorKind.Data,
        SensorType.SmallData => SensorKind.SmallData,
        SensorType.Throughput => SensorKind.Throughput,
        _ => SensorKind.Other
    };

    private static string GetUnit(SensorType type) => type switch
    {
        SensorType.Voltage => "V",
        SensorType.Clock => "MHz",
        SensorType.Temperature => "°C",
        SensorType.Load => "%",
        SensorType.Fan => "RPM",
        SensorType.Flow => "L/h",
        SensorType.Power => "W",
        SensorType.Data => "GB",
        SensorType.SmallData => "MB",
        SensorType.Throughput => "B/s",
        _ => string.Empty
    };

    private static void ApplySmartDefaults(SensorItem sensor)
    {
        // CPU
        if (sensor.Category == HardwareCategory.Cpu)
        {
            if (sensor.Kind == SensorKind.Temperature && (sensor.OriginalName.Contains("Package") || sensor.OriginalName.Contains("Tdie") || sensor.OriginalName.Contains("Core")))
            {
                sensor.CustomName = "CPU Sıcaklığı";
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.GlowBar;
                sensor.WarningThreshold = 75;
                sensor.CriticalThreshold = 85;
                sensor.IsAudioAlertEnabled = true;
            }
            else if (sensor.Kind == SensorKind.Clock && (sensor.OriginalName.Contains("Core #1") || sensor.OriginalName.Contains("Average") || sensor.OriginalName.Contains("Core (Max)")))
            {
                sensor.CustomName = "CPU Frekansı";
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.CircularGauge;
            }
            else if (sensor.Kind == SensorKind.Load && (sensor.OriginalName.Contains("Total") || sensor.OriginalName.Contains("CPU Total")))
            {
                sensor.CustomName = "CPU Kullanımı";
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.GlowBar;
                sensor.WarningThreshold = 80;
                sensor.CriticalThreshold = 95;
            }
        }
        // GPU
        else if (sensor.Category == HardwareCategory.Gpu)
        {
            if (sensor.Kind == SensorKind.Clock && sensor.OriginalName.Contains("Core"))
            {
                sensor.CustomName = "GPU Çekirdek Hızı";
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.CircularGauge;
            }
            else if (sensor.Kind == SensorKind.Temperature && (sensor.OriginalName.Contains("Core") || sensor.OriginalName.Contains("GPU")))
            {
                sensor.CustomName = "GPU Sıcaklığı";
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.GlowBar;
                sensor.WarningThreshold = 75;
                sensor.CriticalThreshold = 85;
                sensor.IsAudioAlertEnabled = true;
            }
            else if (sensor.Kind == SensorKind.Load && sensor.OriginalName.Contains("Core"))
            {
                sensor.CustomName = "GPU Kullanımı";
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.GlowBar;
                sensor.WarningThreshold = 80;
                sensor.CriticalThreshold = 95;
            }
        }
        // RAM
        else if (sensor.Category == HardwareCategory.Memory)
        {
            if (sensor.Kind == SensorKind.Load)
            {
                sensor.CustomName = "RAM Kullanımı";
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.CapacityBar;
                sensor.WarningThreshold = 85;
                sensor.CriticalThreshold = 95;
            }
        }
        // Storage
        else if (sensor.Category == HardwareCategory.Storage)
        {
            if (sensor.Kind == SensorKind.Temperature)
            {
                sensor.CustomName = $"{sensor.HardwareName} Sıcaklık";
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.KeyValue;
                sensor.WarningThreshold = 65;
                sensor.CriticalThreshold = 75;
            }
            else if (sensor.Kind == SensorKind.Load)
            {
                sensor.CustomName = $"{sensor.HardwareName} Doluluk";
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.CapacityBar;
            }
        }
        // Motherboard Fans
        else if (sensor.Category == HardwareCategory.Motherboard || sensor.Category == HardwareCategory.Controller)
        {
            if (sensor.Kind == SensorKind.Fan)
            {
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.KeyValue;
            }
            else if (sensor.Kind == SensorKind.Temperature)
            {
                sensor.IsSelected = true;
                sensor.IsDefaultRecommended = true;
                sensor.WidgetType = WidgetType.KeyValue;
                sensor.WarningThreshold = 60;
                sensor.CriticalThreshold = 75;
            }
        }
    }

    private void GenerateFallbackSensors(List<SensorItem> list)
    {
        // CPU
        list.Add(new SensorItem
        {
            Id = "/cpu/0/clock/0",
            HardwareId = "/cpu/0",
            HardwareName = "AMD Ryzen / Intel Core CPU",
            Category = HardwareCategory.Cpu,
            Kind = SensorKind.Clock,
            OriginalName = "CPU Core #1 Frequency",
            CustomName = "CPU Frekansı",
            Unit = "MHz",
            Value = 5225,
            Min = 3200,
            Max = 5600,
            WidgetType = WidgetType.CircularGauge,
            IsSelected = true,
            IsDefaultRecommended = true
        });

        list.Add(new SensorItem
        {
            Id = "/cpu/0/temperature/0",
            HardwareId = "/cpu/0",
            HardwareName = "AMD Ryzen / Intel Core CPU",
            Category = HardwareCategory.Cpu,
            Kind = SensorKind.Temperature,
            OriginalName = "Core (Tctl/Tdie)",
            CustomName = "CPU Sıcaklığı",
            Unit = "°C",
            Value = 52,
            Min = 38,
            Max = 88,
            WidgetType = WidgetType.GlowBar,
            IsSelected = true,
            IsDefaultRecommended = true,
            WarningThreshold = 75,
            CriticalThreshold = 85,
            IsAudioAlertEnabled = true
        });

        list.Add(new SensorItem
        {
            Id = "/cpu/0/load/0",
            HardwareId = "/cpu/0",
            HardwareName = "AMD Ryzen / Intel Core CPU",
            Category = HardwareCategory.Cpu,
            Kind = SensorKind.Load,
            OriginalName = "CPU Total Load",
            CustomName = "CPU Kullanımı",
            Unit = "%",
            Value = 18,
            Min = 1,
            Max = 100,
            WidgetType = WidgetType.GlowBar,
            IsSelected = true,
            IsDefaultRecommended = true,
            WarningThreshold = 80,
            CriticalThreshold = 95
        });

        // GPU
        list.Add(new SensorItem
        {
            Id = "/gpu/0/clock/0",
            HardwareId = "/gpu/0",
            HardwareName = "NVIDIA GeForce RTX 5080 16G GAMING TRIO Series",
            Category = HardwareCategory.Gpu,
            Kind = SensorKind.Clock,
            OriginalName = "GPU Core Clock",
            CustomName = "GPU Çekirdek Hızı",
            Unit = "MHz",
            Value = 2737,
            Min = 210,
            Max = 2950,
            WidgetType = WidgetType.CircularGauge,
            IsSelected = true,
            IsDefaultRecommended = true
        });

        list.Add(new SensorItem
        {
            Id = "/gpu/0/load/0",
            HardwareId = "/gpu/0",
            HardwareName = "NVIDIA GeForce RTX 5080 16G GAMING TRIO Series",
            Category = HardwareCategory.Gpu,
            Kind = SensorKind.Load,
            OriginalName = "GPU Core Load",
            CustomName = "GPU Kullanımı",
            Unit = "%",
            Value = 98,
            Min = 0,
            Max = 100,
            WidgetType = WidgetType.GlowBar,
            IsSelected = true,
            IsDefaultRecommended = true,
            WarningThreshold = 80,
            CriticalThreshold = 95
        });

        list.Add(new SensorItem
        {
            Id = "/gpu/0/temperature/0",
            HardwareId = "/gpu/0",
            HardwareName = "NVIDIA GeForce RTX 5080 16G GAMING TRIO Series",
            Category = HardwareCategory.Gpu,
            Kind = SensorKind.Temperature,
            OriginalName = "GPU Core Temperature",
            CustomName = "GPU Sıcaklığı",
            Unit = "°C",
            Value = 61,
            Min = 32,
            Max = 82,
            WidgetType = WidgetType.GlowBar,
            IsSelected = true,
            IsDefaultRecommended = true,
            WarningThreshold = 75,
            CriticalThreshold = 85,
            IsAudioAlertEnabled = true
        });

        // Memory
        list.Add(new SensorItem
        {
            Id = "/ram/0/load/0",
            HardwareId = "/ram/0",
            HardwareName = "Generic DDR5 RAM",
            Category = HardwareCategory.Memory,
            Kind = SensorKind.Load,
            OriginalName = "Memory Used Percentage",
            CustomName = "RAM Kullanımı",
            Unit = "%",
            Value = 58,
            Min = 20,
            Max = 95,
            WidgetType = WidgetType.CapacityBar,
            IsSelected = true,
            IsDefaultRecommended = true,
            WarningThreshold = 85,
            CriticalThreshold = 95
        });

        // Storage
        list.Add(new SensorItem
        {
            Id = "/nvme/0/temperature/0",
            HardwareId = "/nvme/0",
            HardwareName = "NVMe SSD (Samsung 990 Pro 2TB)",
            Category = HardwareCategory.Storage,
            Kind = SensorKind.Temperature,
            OriginalName = "Drive Temperature",
            CustomName = "NVMe SSD Sıcaklık",
            Unit = "°C",
            Value = 41,
            Min = 30,
            Max = 70,
            WidgetType = WidgetType.KeyValue,
            IsSelected = true,
            IsDefaultRecommended = true,
            WarningThreshold = 65,
            CriticalThreshold = 75
        });

        list.Add(new SensorItem
        {
            Id = "/nvme/0/load/0",
            HardwareId = "/nvme/0",
            HardwareName = "NVMe SSD (Samsung 990 Pro 2TB)",
            Category = HardwareCategory.Storage,
            Kind = SensorKind.Load,
            OriginalName = "Used Space",
            CustomName = "NVMe SSD Doluluk",
            Unit = "%",
            Value = 74,
            Min = 0,
            Max = 100,
            WidgetType = WidgetType.CapacityBar,
            IsSelected = true,
            IsDefaultRecommended = true
        });

        // Fans
        list.Add(new SensorItem
        {
            Id = "/motherboard/0/fan/0",
            HardwareId = "/motherboard/0",
            HardwareName = "Motherboard Super I/O",
            Category = HardwareCategory.Motherboard,
            Kind = SensorKind.Fan,
            OriginalName = "CPU Fan 1",
            CustomName = "CPU Fan 1",
            Unit = "RPM",
            Value = 1572,
            Min = 600,
            Max = 2200,
            WidgetType = WidgetType.KeyValue,
            IsSelected = true,
            IsDefaultRecommended = true
        });

        list.Add(new SensorItem
        {
            Id = "/motherboard/0/fan/1",
            HardwareId = "/motherboard/0",
            HardwareName = "Motherboard Super I/O",
            Category = HardwareCategory.Motherboard,
            Kind = SensorKind.Fan,
            OriginalName = "PUMP SYS Fan 1",
            CustomName = "Sıvı Soğutma Pompa (PUMP SYS)",
            Unit = "RPM",
            Value = 3603,
            Min = 1000,
            Max = 4200,
            WidgetType = WidgetType.KeyValue,
            IsSelected = true,
            IsDefaultRecommended = true
        });

        list.Add(new SensorItem
        {
            Id = "/motherboard/0/fan/2",
            HardwareId = "/motherboard/0",
            HardwareName = "Motherboard Super I/O",
            Category = HardwareCategory.Motherboard,
            Kind = SensorKind.Fan,
            OriginalName = "SYS Fan 1",
            CustomName = "Kasa Ön Fan 1 (SYS Fan 1)",
            Unit = "RPM",
            Value = 1253,
            Min = 400,
            Max = 1800,
            WidgetType = WidgetType.KeyValue,
            IsSelected = true,
            IsDefaultRecommended = true
        });

        list.Add(new SensorItem
        {
            Id = "/motherboard/0/fan/3",
            HardwareId = "/motherboard/0",
            HardwareName = "Motherboard Super I/O",
            Category = HardwareCategory.Motherboard,
            Kind = SensorKind.Fan,
            OriginalName = "SYS Fan 5",
            CustomName = "Kasa Egzoz Fan (SYS Fan 5)",
            Unit = "RPM",
            Value = 1057,
            Min = 400,
            Max = 1800,
            WidgetType = WidgetType.KeyValue,
            IsSelected = true,
            IsDefaultRecommended = true
        });

        list.Add(new SensorItem
        {
            Id = "/motherboard/0/temperature/0",
            HardwareId = "/motherboard/0",
            HardwareName = "Motherboard Super I/O",
            Category = HardwareCategory.Motherboard,
            Kind = SensorKind.Temperature,
            OriginalName = "Motherboard Ambient",
            CustomName = "Kasa İçi Sıcaklık",
            Unit = "°C",
            Value = 34,
            Min = 25,
            Max = 55,
            WidgetType = WidgetType.KeyValue,
            IsSelected = true,
            IsDefaultRecommended = true,
            WarningThreshold = 50,
            CriticalThreshold = 65
        });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pollingTimer.Stop();
        _pollingTimer.Dispose();

        try
        {
            _computer.Close();
        }
        catch { }
    }
}
