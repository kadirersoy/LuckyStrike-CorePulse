namespace CorePulse.Core.Models;

public enum WidgetType
{
    CircularGauge,
    GlowBar,
    Sparkline,
    KeyValue,
    CapacityBar
}

public enum HardwareCategory
{
    Cpu,
    Gpu,
    Motherboard,
    Memory,
    Storage,
    Network,
    Controller,
    Other
}

public enum SensorKind
{
    Temperature,
    Load,
    Clock,
    Fan,
    Flow,
    Power,
    Voltage,
    Data,
    SmallData,
    Factor,
    Throughput,
    Level,
    Energy,
    Other
}
