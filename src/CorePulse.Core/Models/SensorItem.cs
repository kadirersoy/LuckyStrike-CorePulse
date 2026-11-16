using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CorePulse.Core.Models;

public class SensorItem : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _hardwareId = string.Empty;
    private string _hardwareName = string.Empty;
    private HardwareCategory _category = HardwareCategory.Other;
    private SensorKind _sensorKind = SensorKind.Other;
    private string _originalName = string.Empty;
    private string _customName = string.Empty;
    private string _unit = string.Empty;
    private float? _value;
    private float? _min;
    private float? _max;
    private WidgetType _widgetType = WidgetType.KeyValue;
    private bool _isSelected;
    private bool _isDefaultRecommended;
    private float? _warningThreshold;
    private float? _criticalThreshold;
    private bool _isAudioAlertEnabled;
    private bool _isWarning;
    private bool _isCritical;
    private int _sortOrder;
    private readonly Queue<float> _historyQueue = new(60);
    private string _historyPoints = string.Empty;

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string HardwareId
    {
        get => _hardwareId;
        set => SetField(ref _hardwareId, value);
    }

    public string HardwareName
    {
        get => _hardwareName;
        set => SetField(ref _hardwareName, value);
    }

    public HardwareCategory Category
    {
        get => _category;
        set => SetField(ref _category, value);
    }

    public SensorKind Kind
    {
        get => _sensorKind;
        set => SetField(ref _sensorKind, value);
    }

    public string OriginalName
    {
        get => _originalName;
        set => SetField(ref _originalName, value);
    }

    public string CustomName
    {
        get => string.IsNullOrWhiteSpace(_customName) ? _originalName : _customName;
        set
        {
            if (SetField(ref _customName, value))
            {
                OnPropertyChanged(nameof(DisplayName));
            }
        }
    }

    public string DisplayName => string.IsNullOrWhiteSpace(_customName) ? _originalName : _customName;

    public string Unit
    {
        get => _unit;
        set => SetField(ref _unit, value);
    }

    public float? Value
    {
        get => _value;
        set
        {
            if (SetField(ref _value, value))
            {
                OnPropertyChanged(nameof(FormattedValue));
                OnPropertyChanged(nameof(FormattedFull));
                OnPropertyChanged(nameof(PercentageValue));
                UpdateThresholdStatus();
                if (value.HasValue)
                {
                    AddHistoryPoint(value.Value);
                }
            }
        }
    }

    public float? Min
    {
        get => _min;
        set => SetField(ref _min, value);
    }

    public float? Max
    {
        get => _max;
        set => SetField(ref _max, value);
    }

    public WidgetType WidgetType
    {
        get => _widgetType;
        set => SetField(ref _widgetType, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public bool IsDefaultRecommended
    {
        get => _isDefaultRecommended;
        set => SetField(ref _isDefaultRecommended, value);
    }

    public float? WarningThreshold
    {
        get => _warningThreshold;
        set
        {
            if (SetField(ref _warningThreshold, value))
            {
                UpdateThresholdStatus();
            }
        }
    }

    public float? CriticalThreshold
    {
        get => _criticalThreshold;
        set
        {
            if (SetField(ref _criticalThreshold, value))
            {
                UpdateThresholdStatus();
            }
        }
    }

    public bool IsAudioAlertEnabled
    {
        get => _isAudioAlertEnabled;
        set => SetField(ref _isAudioAlertEnabled, value);
    }

    public bool IsWarning
    {
        get => _isWarning;
        set => SetField(ref _isWarning, value);
    }

    public bool IsCritical
    {
        get => _isCritical;
        set => SetField(ref _isCritical, value);
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => SetField(ref _sortOrder, value);
    }

    public string FormattedValue
    {
        get
        {
            if (!_value.HasValue) return "--";
            if (Kind == SensorKind.Temperature || Kind == SensorKind.Load || Kind == SensorKind.Fan)
                return _value.Value.ToString("F0");
            if (Kind == SensorKind.Clock)
                return _value.Value.ToString("F0");
            if (Kind == SensorKind.Power || Kind == SensorKind.Voltage)
                return _value.Value.ToString("F1");
            return _value.Value.ToString("F1");
        }
    }

    public string FormattedFull => $"{FormattedValue} {Unit}".Trim();

    public double PercentageValue
    {
        get
        {
            if (!_value.HasValue) return 0.0;
            if (Kind == SensorKind.Load)
                return Math.Clamp(_value.Value, 0f, 100f);
            if (Kind == SensorKind.Temperature)
                return Math.Clamp(_value.Value, 0f, 110f) / 110.0 * 100.0;
            if (Kind == SensorKind.Clock && Max.HasValue && Max.Value > 0)
                return Math.Clamp((_value.Value / Max.Value) * 100f, 0f, 100f);
            if (Kind == SensorKind.Fan && Max.HasValue && Max.Value > 0)
                return Math.Clamp((_value.Value / Max.Value) * 100f, 0f, 100f);
            return Math.Clamp(_value.Value, 0f, 100f);
        }
    }

    public string HistoryPoints
    {
        get => _historyPoints;
        private set => SetField(ref _historyPoints, value);
    }

    private void AddHistoryPoint(float val)
    {
        if (_historyQueue.Count >= 60)
        {
            _historyQueue.Dequeue();
        }
        _historyQueue.Enqueue(val);

        // Generate normalized SVG/Path points (0 to 100 scale)
        if (_historyQueue.Count > 1)
        {
            float minVal = _historyQueue.Min();
            float maxVal = _historyQueue.Max();
            float range = Math.Max(1.0f, maxVal - minVal);

            var points = _historyQueue.Select((v, index) =>
            {
                double x = (index / (double)(Math.Max(1, _historyQueue.Count - 1))) * 300.0;
                double normalizedY = (v - minVal) / range;
                double y = 70.0 - (normalizedY * 60.0); // Inverted for Y-down coordinates
                return $"{x:F1},{y:F1}";
            });

            HistoryPoints = string.Join(" ", points);
        }
    }

    private void UpdateThresholdStatus()
    {
        if (!_value.HasValue)
        {
            IsCritical = false;
            IsWarning = false;
            return;
        }

        if (CriticalThreshold.HasValue && _value.Value >= CriticalThreshold.Value)
        {
            IsCritical = true;
            IsWarning = false;
        }
        else if (WarningThreshold.HasValue && _value.Value >= WarningThreshold.Value)
        {
            IsCritical = false;
            IsWarning = true;
        }
        else
        {
            IsCritical = false;
            IsWarning = false;
        }
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
