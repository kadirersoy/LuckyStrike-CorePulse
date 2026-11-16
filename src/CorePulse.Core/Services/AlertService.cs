using CorePulse.Core.Models;

namespace CorePulse.Core.Services;

public class AlertService
{
    private readonly Dictionary<string, DateTime> _lastAlertTimes = new();
    private DateTime? _snoozeUntil;

    public bool MasterAudioAlertEnabled { get; set; } = true;
    public int CooldownSeconds { get; set; } = 15;

    public bool IsSnoozed => _snoozeUntil.HasValue && DateTime.UtcNow < _snoozeUntil.Value;

    public void Snooze(TimeSpan duration)
    {
        _snoozeUntil = DateTime.UtcNow.Add(duration);
    }

    public void ClearSnooze()
    {
        _snoozeUntil = null;
    }

    public void TriggerAlert(SensorItem sensor, bool isCritical)
    {
        if (!MasterAudioAlertEnabled || IsSnoozed) return;
        if (!sensor.IsAudioAlertEnabled) return;

        // Check per-sensor cooldown
        if (_lastAlertTimes.TryGetValue(sensor.Id, out var lastTime))
        {
            if ((DateTime.UtcNow - lastTime).TotalSeconds < CooldownSeconds)
            {
                return;
            }
        }

        _lastAlertTimes[sensor.Id] = DateTime.UtcNow;

        // Play warning or critical sound asynchronously without blocking UI
        Task.Run(() =>
        {
            try
            {
                if (isCritical)
                {
                    Console.Beep(1200, 180);
                    Thread.Sleep(50);
                    Console.Beep(1200, 220);
                }
                else
                {
                    Console.Beep(750, 150);
                }
            }
            catch
            {
                // In case sound hardware is unavailable
            }
        });
    }
}
