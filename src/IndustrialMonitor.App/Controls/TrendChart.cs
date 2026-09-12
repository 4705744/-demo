using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

namespace IndustrialMonitor.App.Controls;

public sealed class TrendChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(nameof(Values), typeof(INotifyCollectionChanged), typeof(TrendChart), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));
    public INotifyCollectionChanged? Values { get => (INotifyCollectionChanged?)GetValue(ValuesProperty); set => SetValue(ValuesProperty, value); }

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var chart = (TrendChart)d;
        if (e.OldValue is INotifyCollectionChanged oldValue) oldValue.CollectionChanged -= chart.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newValue) newValue.CollectionChanged += chart.OnCollectionChanged;
        chart.InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = ActualWidth; var h = ActualHeight;
        if (w <= 1 || h <= 1) return;
        var bg = new SolidColorBrush(Color.FromRgb(16, 27, 39)); bg.Freeze();
        var grid = new SolidColorBrush(Color.FromArgb(90, 54, 73, 90)); grid.Freeze();
        var line = new SolidColorBrush(Color.FromRgb(80, 210, 194)); line.Freeze();
        dc.DrawRoundedRectangle(bg, null, new Rect(0, 0, w, h), 8, 8);
        var gridPen = new Pen(grid, 1);
        for (var i = 1; i < 5; i++) { var y = h * i / 5d; dc.DrawLine(gridPen, new Point(0, y), new Point(w, y)); }
        if (Values is not IEnumerable<double> source) return;
        var values = source.ToArray();
        if (values.Length < 2) return;
        var min = values.Min(); var max = values.Max(); if (Math.Abs(max - min) < .001) { min -= 1; max += 1; }
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            for (var i = 0; i < values.Length; i++)
            {
                var x = w * i / Math.Max(1, values.Length - 1d);
                var y = h - 10 - ((values[i] - min) / (max - min)) * Math.Max(1, h - 20);
                var p = new Point(x, y);
                if (i == 0) ctx.BeginFigure(p, false, false); else ctx.LineTo(p, true, false);
            }
        }
        geometry.Freeze();
        dc.DrawGeometry(null, new Pen(line, 2), geometry);
    }
}
