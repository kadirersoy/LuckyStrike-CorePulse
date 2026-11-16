using CorePulse.Core.Models;

namespace CorePulse.Core.Services;

public interface ISensorService : IDisposable
{
    IReadOnlyList<SensorItem> DiscoveredSensors { get; }
    bool IsRunning { get; }
    void Initialize();
    void DiscoverSensors(bool preserveExistingSettings = true);
    void UpdateSensors();
    void SetPollingInterval(int intervalMs);
    void StartPolling();
    void StopPolling();

    event Action<IReadOnlyList<SensorItem>>? ValuesUpdated;
    event Action<SensorItem, bool /* isCritical */>? ThresholdExceeded;
}
