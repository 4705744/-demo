namespace IndustrialMonitor.App.Models;

public sealed record DeviceReading(DateTime Timestamp,double Temperature,double Pressure,double Speed,double Vibration,long ProductionCount);

public sealed class AlarmRecord
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = "Warning";
    public string Metric { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsRecovery { get; init; }
    public bool IsAcknowledged { get; set; }
}

public enum DeviceConnectionState { Disconnected, Connecting, Connected, Reconnecting, Faulted }
