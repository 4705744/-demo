using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Input;

namespace IndustrialMonitor.App.Infrastructure;

public static class AppPaths
{
    public static string RootDirectory { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GenericIndustrialMonitorDemo");
    public static string LogDirectory { get; } = Path.Combine(RootDirectory, "logs");
    public static string DataDirectory { get; } = Path.Combine(RootDirectory, "data");

    static AppPaths()
    {
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(DataDirectory);
    }
}

public static class AppLogger
{
    private static readonly object SyncRoot = new();
    public static event EventHandler<string>? MessageLogged;
    public static void Info(string message) => Write("INFO", message, null);
    public static void Warn(string message) => Write("WARN", message, null);
    public static void Error(string message, Exception? exception = null) => Write("ERROR", message, exception);

    private static void Write(string level, string message, Exception? exception)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            if (exception is not null) line += Environment.NewLine + exception;
            lock (SyncRoot)
            {
                var file = Path.Combine(AppPaths.LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(file, line + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            Debug.WriteLine(line);
            MessageLogged?.Invoke(null, line);
        }
        catch { }
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;
    public RelayCommand(Action execute, Func<bool>? canExecute = null) : this(_ => execute(), canExecute is null ? null : _ => canExecute()) { }
    public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
    public void Execute(object? parameter) => _execute(parameter);
    public event EventHandler? CanExecuteChanged;
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public static class CsvUtility
{
    public static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    public static double ParseDouble(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;
    public static long ParseLong(string value) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
