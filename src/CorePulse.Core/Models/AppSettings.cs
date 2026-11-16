namespace CorePulse.Core.Models;

public class SensorConfigEntry
{
    public string SensorId { get; set; } = string.Empty;
    public string CustomName { get; set; } = string.Empty;
    public WidgetType WidgetType { get; set; } = WidgetType.KeyValue;
    public bool IsSelected { get; set; }
    public float? WarningThreshold { get; set; }
    public float? CriticalThreshold { get; set; }
    public bool IsAudioAlertEnabled { get; set; }
    public int SortOrder { get; set; }
}

public class AppSettings
{
    public bool IsFirstRun { get; set; } = true;
    public int PollingIntervalMs { get; set; } = 1000;
    public bool MasterAudioAlertEnabled { get; set; } = true;
    public int AudioAlertCooldownSeconds { get; set; } = 15;
    public List<SensorConfigEntry> SensorConfigs { get; set; } = new();
}
