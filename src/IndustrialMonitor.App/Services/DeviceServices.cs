using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using IndustrialMonitor.App.Infrastructure;
using IndustrialMonitor.App.Models;

namespace IndustrialMonitor.App.Services;

public interface IDeviceClient : IAsyncDisposable
{
    event EventHandler<DeviceReading>? DataReceived;
    event EventHandler<DeviceConnectionState>? StateChanged;
    bool IsConnected { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}

public sealed class SimulatedDeviceClient : IDeviceClient
{
    private readonly Random _random = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private long _productionCount;
    private double _phase;

    public event EventHandler<DeviceReading>? DataReceived;
    public event EventHandler<DeviceConnectionState>? StateChanged;
    public bool IsConnected { get; private set; }

    public Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected) return Task.CompletedTask;
        StateChanged?.Invoke(this, DeviceConnectionState.Connecting);
        IsConnected = true;
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _loopTask = Task.Run(() => RunAsync(_cts.Token), _cts.Token);
        StateChanged?.Invoke(this, DeviceConnectionState.Connected);
        AppLogger.Info("Simulated device connected.");
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                _phase += 0.12;
                _productionCount += _random.Next(0, 3);
                var pulse = Math.Sin(_phase / 5.5) > 0.94 ? 18 : 0;
                double Noise(double amp) => (_random.NextDouble() - .5) * 2 * amp;
                var reading = new DeviceReading(DateTime.Now,
                    Math.Round(60 + Math.Sin(_phase) * 7 + pulse + Noise(1.4), 1),
                    Math.Round(.68 + Math.Sin(_phase * .65) * .08 + Noise(.015), 3),
                    Math.Round(1450 + Math.Sin(_phase * .42) * 95 + Noise(18)),
                    Math.Round(2.1 + Math.Abs(Math.Sin(_phase * 1.4)) * 1.7 + (pulse > 0 ? 5.2 : 0) + Noise(.25), 2),
                    _productionCount);
                DataReceived?.Invoke(this, reading);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLogger.Error("Simulated device loop failed.", ex);
            StateChanged?.Invoke(this, DeviceConnectionState.Faulted);
        }
    }

    public async Task DisconnectAsync()
    {
        if (!IsConnected) return;
        IsConnected = false;
        _cts?.Cancel();
        if (_loopTask is not null) { try { await _loopTask; } catch (OperationCanceledException) { } }
        _cts?.Dispose(); _cts = null; _loopTask = null;
        StateChanged?.Invoke(this, DeviceConnectionState.Disconnected);
        AppLogger.Info("Simulated device disconnected.");
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}

public sealed class TcpJsonDeviceClient : IDeviceClient
{
    private readonly string _host;
    private readonly int _port;
    private TcpClient? _client;
    private CancellationTokenSource? _cts;
    private Task? _readTask;

    public TcpJsonDeviceClient(string host, int port) { _host = host; _port = port; }
    public event EventHandler<DeviceReading>? DataReceived;
    public event EventHandler<DeviceConnectionState>? StateChanged;
    public bool IsConnected => _client?.Connected == true;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (IsConnected) return;
        StateChanged?.Invoke(this, DeviceConnectionState.Connecting);
        _client = new TcpClient();
        try
        {
            await _client.ConnectAsync(_host, _port, cancellationToken);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
            StateChanged?.Invoke(this, DeviceConnectionState.Connected);
            AppLogger.Info($"TCP device connected: {_host}:{_port}");
        }
        catch
        {
            _client.Dispose(); _client = null;
            StateChanged?.Invoke(this, DeviceConnectionState.Faulted);
            throw;
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_client is null) return;
            using var reader = new StreamReader(_client.GetStream(), System.Text.Encoding.UTF8, false, 4096, true);
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) break;
                if (TryParse(line, out var reading)) DataReceived?.Invoke(this, reading);
                else AppLogger.Warn($"Ignored invalid TCP payload: {line}");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLogger.Error("TCP receive loop failed.", ex);
            StateChanged?.Invoke(this, DeviceConnectionState.Faulted);
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested) StateChanged?.Invoke(this, DeviceConnectionState.Disconnected);
        }
    }

    private static bool TryParse(string json, out DeviceReading reading)
    {
        reading = new DeviceReading(DateTime.Now, 0, 0, 0, 0, 0);
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            double D(string n) => r.TryGetProperty(n, out var v) && v.TryGetDouble(out var x) ? x : 0;
            long L(string n) => r.TryGetProperty(n, out var v) && v.TryGetInt64(out var x) ? x : 0;
            reading = new DeviceReading(DateTime.Now, D("temperature"), D("pressure"), D("speed"), D("vibration"), L("productionCount"));
            return true;
        }
        catch (JsonException) { return false; }
    }

    public async Task DisconnectAsync()
    {
        _cts?.Cancel(); _client?.Close();
        if (_readTask is not null) { try { await _readTask; } catch (OperationCanceledException) { } }
        _cts?.Dispose(); _cts = null; _readTask = null; _client?.Dispose(); _client = null;
        StateChanged?.Invoke(this, DeviceConnectionState.Disconnected);
        AppLogger.Info("TCP device disconnected.");
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();
}
