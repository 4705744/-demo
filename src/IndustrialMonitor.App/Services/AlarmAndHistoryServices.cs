using System.IO;
using IndustrialMonitor.App.Infrastructure;
using IndustrialMonitor.App.Models;

namespace IndustrialMonitor.App.Services;

public sealed class AlarmEngine
{
    private readonly HashSet<string> _active = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<AlarmRecord> Evaluate(DeviceReading reading)
    {
        foreach (var a in Check("TemperatureHigh", reading.Temperature > 75, "High", "温度", $"温度过高：{reading.Temperature:0.0} °C")) yield return a;
        foreach (var a in Check("PressureHigh", reading.Pressure > .85, "Warning", "压力", $"压力偏高：{reading.Pressure:0.000} MPa")) yield return a;
        foreach (var a in Check("VibrationHigh", reading.Vibration > 6.5, "High", "振动", $"振动过高：{reading.Vibration:0.00} mm/s")) yield return a;
    }

    private IEnumerable<AlarmRecord> Check(string key, bool condition, string level, string metric, string message)
    {
        if (condition && _active.Add(key))
            yield return new AlarmRecord { Timestamp = DateTime.Now, Level = level, Metric = metric, Message = message };
        else if (!condition && _active.Remove(key))
            yield return new AlarmRecord { Timestamp = DateTime.Now, Level = "Recovery", Metric = metric, Message = $"{metric}已恢复正常", IsRecovery = true, IsAcknowledged = true };
    }
}

public sealed class CsvHistoryStore
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task AppendAsync(DeviceReading reading)
    {
        await _gate.WaitAsync();
        try
        {
            var path = Path.Combine(AppPaths.DataDirectory, $"history-{reading.Timestamp:yyyyMMdd}.csv");
            var isNew = !File.Exists(path);
            await using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            await using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8);
            if (isNew) await writer.WriteLineAsync("Timestamp,Temperature,Pressure,Speed,Vibration,ProductionCount");
            await writer.WriteLineAsync(string.Join(',', reading.Timestamp.ToString("O"), CsvUtility.Format(reading.Temperature), CsvUtility.Format(reading.Pressure), CsvUtility.Format(reading.Speed), CsvUtility.Format(reading.Vibration), reading.ProductionCount));
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<DeviceReading>> LoadLatestAsync(int maxCount = 100)
    {
        var result = new List<DeviceReading>();
        foreach (var file in Directory.GetFiles(AppPaths.DataDirectory, "history-*.csv").OrderByDescending(x => x).Take(3))
        {
            string[] lines;
            try { lines = await File.ReadAllLinesAsync(file); } catch (IOException) { continue; }
            foreach (var line in lines.Skip(1).Reverse())
            {
                var p = line.Split(',');
                if (p.Length != 6 || !DateTime.TryParse(p[0], out var t)) continue;
                result.Add(new DeviceReading(t, CsvUtility.ParseDouble(p[1]), CsvUtility.ParseDouble(p[2]), CsvUtility.ParseDouble(p[3]), CsvUtility.ParseDouble(p[4]), CsvUtility.ParseLong(p[5])));
                if (result.Count >= maxCount) return result;
            }
        }
        return result;
    }
}
