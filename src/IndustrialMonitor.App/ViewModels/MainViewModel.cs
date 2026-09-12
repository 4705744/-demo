using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using IndustrialMonitor.App.Infrastructure;
using IndustrialMonitor.App.Models;
using IndustrialMonitor.App.Services;

namespace IndustrialMonitor.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly AlarmEngine _alarmEngine = new();
    private readonly CsvHistoryStore _historyStore = new();
    private IDeviceClient? _deviceClient;
    private DateTime _lastHistoryWrite = DateTime.MinValue;
    private bool _isConnected;
    private string _connectionState = "未连接";
    private string _selectedProtocol = "模拟设备";
    private string _host = "127.0.0.1";
    private string _port = "9000";
    private double _temperature;
    private double _pressure;
    private double _speed;
    private double _vibration;
    private long _productionCount;
    private string _lastUpdate = "--";

    public MainViewModel()
    {
        Protocols = new ObservableCollection<string> { "模拟设备", "TCP JSON" };
        TemperatureTrend = new ObservableCollection<double>();
        Alarms = new ObservableCollection<AlarmRecord>();
        History = new ObservableCollection<DeviceReading>();
        Logs = new ObservableCollection<string>();
        ConnectCommand = new RelayCommand(async () => await ConnectOrDisconnectAsync());
        AcknowledgeAllCommand = new RelayCommand(AcknowledgeAll);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        AppLogger.MessageLogged += OnLogMessage;
        _ = LoadInitialHistoryAsync();
    }

    public ObservableCollection<string> Protocols { get; }
    public ObservableCollection<double> TemperatureTrend { get; }
    public ObservableCollection<AlarmRecord> Alarms { get; }
    public ObservableCollection<DeviceReading> History { get; }
    public ObservableCollection<string> Logs { get; }
    public RelayCommand ConnectCommand { get; }
    public RelayCommand AcknowledgeAllCommand { get; }
    public RelayCommand OpenDataFolderCommand { get; }

    public string SelectedProtocol { get => _selectedProtocol; set { if (SetField(ref _selectedProtocol, value)) OnPropertyChanged(nameof(IsTcp)); } }
    public string Host { get => _host; set => SetField(ref _host, value); }
    public string Port { get => _port; set => SetField(ref _port, value); }
    public bool IsTcp => SelectedProtocol == "TCP JSON";
    public bool IsConnected { get => _isConnected; private set { if (SetField(ref _isConnected, value)) { OnPropertyChanged(nameof(ConnectButtonText)); OnPropertyChanged(nameof(ConnectionDot)); } } }
    public string ConnectButtonText => IsConnected ? "断开设备" : "连接设备";
    public string ConnectionDot => IsConnected ? "●" : "○";
    public string ConnectionState { get => _connectionState; private set => SetField(ref _connectionState, value); }
    public double Temperature { get => _temperature; private set => SetField(ref _temperature, value); }
    public double Pressure { get => _pressure; private set => SetField(ref _pressure, value); }
    public double Speed { get => _speed; private set => SetField(ref _speed, value); }
    public double Vibration { get => _vibration; private set => SetField(ref _vibration, value); }
    public long ProductionCount { get => _productionCount; private set => SetField(ref _productionCount, value); }
    public string LastUpdate { get => _lastUpdate; private set => SetField(ref _lastUpdate, value); }

    private async Task ConnectOrDisconnectAsync()
    {
        if (_deviceClient?.IsConnected == true) { await DisconnectCurrentAsync(); return; }
        try
        {
            await DisconnectCurrentAsync();
            _deviceClient = SelectedProtocol == "TCP JSON" ? CreateTcpClient() : new SimulatedDeviceClient();
            _deviceClient.DataReceived += OnDataReceived;
            _deviceClient.StateChanged += OnStateChanged;
            await _deviceClient.ConnectAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to connect device.", ex);
            ConnectionState = "连接失败";
            IsConnected = false;
            MessageBox.Show($"设备连接失败：{ex.Message}", "连接失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private TcpJsonDeviceClient CreateTcpClient()
    {
        if (!int.TryParse(Port, out var port) || port is < 1 or > 65535) throw new InvalidOperationException("端口必须是 1 - 65535 的整数。");
        return new TcpJsonDeviceClient(Host.Trim(), port);
    }

    private void OnStateChanged(object? sender, DeviceConnectionState state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectionState = state switch { DeviceConnectionState.Connecting => "正在连接", DeviceConnectionState.Connected => "已连接", DeviceConnectionState.Reconnecting => "正在重连", DeviceConnectionState.Faulted => "通信异常", _ => "未连接" };
            IsConnected = state == DeviceConnectionState.Connected;
        });
    }

    private void OnDataReceived(object? sender, DeviceReading reading)
    {
        Application.Current.Dispatcher.Invoke(() => ApplyReading(reading));
        _ = PersistReadingAsync(reading);
    }

    private void ApplyReading(DeviceReading reading)
    {
        Temperature = reading.Temperature; Pressure = reading.Pressure; Speed = reading.Speed; Vibration = reading.Vibration; ProductionCount = reading.ProductionCount; LastUpdate = reading.Timestamp.ToString("HH:mm:ss.fff");
        TemperatureTrend.Add(reading.Temperature); while (TemperatureTrend.Count > 120) TemperatureTrend.RemoveAt(0);
        History.Insert(0, reading); while (History.Count > 100) History.RemoveAt(History.Count - 1);
        foreach (var alarm in _alarmEngine.Evaluate(reading))
        {
            Alarms.Insert(0, alarm); while (Alarms.Count > 100) Alarms.RemoveAt(Alarms.Count - 1);
            AppLogger.Warn($"Alarm: {alarm.Message}");
        }
    }

    private async Task PersistReadingAsync(DeviceReading reading)
    {
        if ((reading.Timestamp - _lastHistoryWrite).TotalSeconds < 1) return;
        _lastHistoryWrite = reading.Timestamp;
        try { await _historyStore.AppendAsync(reading); } catch (Exception ex) { AppLogger.Error("Failed to persist history data.", ex); }
    }

    private async Task LoadInitialHistoryAsync()
    {
        try
        {
            var items = await _historyStore.LoadLatestAsync();
            await Application.Current.Dispatcher.InvokeAsync(() => { foreach (var item in items) History.Add(item); });
        }
        catch (Exception ex) { AppLogger.Error("Failed to load history data.", ex); }
    }

    private void AcknowledgeAll()
    {
        foreach (var alarm in Alarms) alarm.IsAcknowledged = true;
        OnPropertyChanged(nameof(Alarms));
        AppLogger.Info("All alarms acknowledged.");
    }

    private static void OpenDataFolder()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = AppPaths.RootDirectory, UseShellExecute = true }); }
        catch (Exception ex) { AppLogger.Error("Failed to open data folder.", ex); }
    }

    private void OnLogMessage(object? sender, string message)
    {
        Application.Current?.Dispatcher.BeginInvoke(() => { Logs.Insert(0, message); while (Logs.Count > 200) Logs.RemoveAt(Logs.Count - 1); });
    }

    private async Task DisconnectCurrentAsync()
    {
        if (_deviceClient is null) return;
        _deviceClient.DataReceived -= OnDataReceived;
        _deviceClient.StateChanged -= OnStateChanged;
        await _deviceClient.DisposeAsync();
        _deviceClient = null;
        IsConnected = false;
        ConnectionState = "未连接";
    }

    public async ValueTask DisposeAsync()
    {
        AppLogger.MessageLogged -= OnLogMessage;
        await DisconnectCurrentAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(name); return true; }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
