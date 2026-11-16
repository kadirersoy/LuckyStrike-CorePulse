using CorePulse.Core.Models;

namespace CorePulse.Core.Services;

public class SensorWebValue
{
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public float Percent { get; set; }
}

public static class TelemetryMapper
{
    public static Dictionary<string, SensorWebValue> MapToWebPayload(IReadOnlyList<SensorItem> sensors)
    {
        var result = new Dictionary<string, SensorWebValue>();

        float? cpuTemp = null, cpuUsage = null, cpuClock = null, cpuPower = null;
        float? gpuTemp = null, gpuUsage = null, gpuClock = null, gpuPower = null, gpuHotspot = null, gpuVram = null;
        float? ramUsage = null, ramUsed = null, ramFree = null;

        foreach (var s in sensors)
        {
            if (s.Category == HardwareCategory.Cpu)
            {
                if (s.Kind == SensorKind.Temperature && (s.OriginalName.Contains("Package") || s.OriginalName.Contains("Tctl") || s.OriginalName.Contains("Core")))
                {
                    if (!cpuTemp.HasValue || (s.Value.HasValue && s.Value > cpuTemp)) cpuTemp = s.Value;
                }
                else if (s.Kind == SensorKind.Load && (s.OriginalName.Contains("Total") || s.OriginalName.Contains("CPU")))
                {
                    cpuUsage = s.Value;
                }
                else if (s.Kind == SensorKind.Clock && s.Value.HasValue)
                {
                    if (!cpuClock.HasValue || s.Value > cpuClock) cpuClock = s.Value;
                }
                else if (s.Kind == SensorKind.Power && s.OriginalName.Contains("Package"))
                {
                    cpuPower = s.Value;
                }
            }
            else if (s.Category == HardwareCategory.Gpu)
            {
                if (s.Kind == SensorKind.Temperature)
                {
                    if (s.OriginalName.Contains("Hot Spot") || s.OriginalName.Contains("Hotspot"))
                        gpuHotspot = s.Value;
                    else if (!gpuTemp.HasValue)
                        gpuTemp = s.Value;
                }
                else if (s.Kind == SensorKind.Load && (s.OriginalName.Contains("Core") || s.OriginalName.Contains("GPU")))
                {
                    gpuUsage = s.Value;
                }
                else if (s.Kind == SensorKind.Clock && s.OriginalName.Contains("Core"))
                {
                    gpuClock = s.Value;
                }
                else if (s.Kind == SensorKind.Power)
                {
                    gpuPower = s.Value;
                }
                else if (s.Kind == SensorKind.Data && s.OriginalName.Contains("Memory Used"))
                {
                    gpuVram = s.Value;
                }
            }
            else if (s.Category == HardwareCategory.Memory)
            {
                if (s.Kind == SensorKind.Load && s.OriginalName.Contains("Memory"))
                    ramUsage = s.Value;
                else if (s.Kind == SensorKind.Data && s.OriginalName.Contains("Used"))
                    ramUsed = s.Value;
                else if (s.Kind == SensorKind.Data && s.OriginalName.Contains("Available"))
                    ramFree = s.Value;
            }
        }

        // Apply CPU
        if (cpuTemp.HasValue)
            result["cpu_temp"] = new SensorWebValue { Value = Math.Round(cpuTemp.Value).ToString(), Unit = "°C", Percent = Math.Clamp(cpuTemp.Value, 0, 100) };
        if (cpuUsage.HasValue)
            result["cpu_usage"] = new SensorWebValue { Value = Math.Round(cpuUsage.Value).ToString(), Unit = "%", Percent = Math.Clamp(cpuUsage.Value, 0, 100) };
        if (cpuClock.HasValue)
            result["cpu_clock"] = new SensorWebValue { Value = Math.Round(cpuClock.Value).ToString(), Unit = "MHz", Percent = Math.Clamp(cpuClock.Value / 5500f * 100, 0, 100) };
        if (cpuPower.HasValue)
            result["cpu_power"] = new SensorWebValue { Value = Math.Round(cpuPower.Value, 1).ToString(), Unit = "W", Percent = Math.Clamp(cpuPower.Value / 120f * 100, 0, 100) };
        if (cpuTemp.HasValue)
            result["cpu_hot_core"] = new SensorWebValue { Value = Math.Round(cpuTemp.Value + 3).ToString(), Unit = "°C", Percent = Math.Clamp(cpuTemp.Value + 3, 0, 100) };

        // Apply GPU
        if (gpuTemp.HasValue)
            result["gpu_temp"] = new SensorWebValue { Value = Math.Round(gpuTemp.Value).ToString(), Unit = "°C", Percent = Math.Clamp(gpuTemp.Value, 0, 100) };
        if (gpuUsage.HasValue)
            result["gpu_usage"] = new SensorWebValue { Value = Math.Round(gpuUsage.Value).ToString(), Unit = "%", Percent = Math.Clamp(gpuUsage.Value, 0, 100) };
        if (gpuClock.HasValue)
            result["gpu_clock"] = new SensorWebValue { Value = Math.Round(gpuClock.Value).ToString(), Unit = "MHz", Percent = Math.Clamp(gpuClock.Value / 3000f * 100, 0, 100) };
        if (gpuHotspot.HasValue)
            result["gpu_hotspot"] = new SensorWebValue { Value = Math.Round(gpuHotspot.Value).ToString(), Unit = "°C", Percent = Math.Clamp(gpuHotspot.Value, 0, 100) };
        if (gpuPower.HasValue)
            result["gpu_power"] = new SensorWebValue { Value = Math.Round(gpuPower.Value, 1).ToString(), Unit = "W", Percent = Math.Clamp(gpuPower.Value / 350f * 100, 0, 100) };
        if (gpuVram.HasValue)
            result["gpu_vram_used"] = new SensorWebValue { Value = Math.Round(gpuVram.Value, 1).ToString(), Unit = "GB", Percent = Math.Clamp(gpuVram.Value / 16f * 100, 0, 100) };

        // Apply RAM
        if (ramUsage.HasValue)
            result["ram_usage"] = new SensorWebValue { Value = Math.Round(ramUsage.Value).ToString(), Unit = "%", Percent = Math.Clamp(ramUsage.Value, 0, 100) };
        if (ramFree.HasValue)
            result["ram_free"] = new SensorWebValue { Value = Math.Round(ramFree.Value, 1).ToString(), Unit = "GB", Percent = Math.Clamp(ramFree.Value / 64f * 100, 0, 100) };

        return result;
    }
}
