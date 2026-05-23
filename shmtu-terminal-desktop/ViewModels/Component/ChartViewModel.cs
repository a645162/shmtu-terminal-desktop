using System.Collections.ObjectModel;
using ReactiveUI;

namespace shmtu.terminal.desktop.ViewModels.Component;

/// <summary>
/// Chart data point for the bar chart component
/// </summary>
public class ChartDataPoint : ViewModelBase
{
    private string _label = "";
    public string Label
    {
        get => _label;
        set => this.RaiseAndSetIfChanged(ref _label, value);
    }

    private double _barHeight;
    public double BarHeight
    {
        get => _barHeight;
        set => this.RaiseAndSetIfChanged(ref _barHeight, value);
    }

    private string _barColor = "#1976D2";
    public string BarColor
    {
        get => _barColor;
        set => this.RaiseAndSetIfChanged(ref _barColor, value);
    }

    private double _value;
    public double Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    public ChartDataPoint() { }

    public ChartDataPoint(string label, double value, double maxHeight = 120)
    {
        _label = label;
        _value = value;
        _barHeight = value >= 0 ? Math.Max(4, value / maxHeight * 100) : 4;
    }
}

/// <summary>
/// ViewModel for the chart component (7-day trend bar chart)
/// XAML bindings: DataPoints (ChartDataPoint with BarColor, BarHeight, Label), IsEmpty
/// </summary>
public class ChartViewModel : ViewModelBase
{
    private const double MaxChartHeight = 120;

    public ObservableCollection<ChartDataPoint> DataPoints { get; } = [];

    private bool _isEmpty = true;
    public bool IsEmpty
    {
        get => _isEmpty;
        set => this.RaiseAndSetIfChanged(ref _isEmpty, value);
    }

    public ChartViewModel() { }

    /// <summary>
    /// Update chart data from a list of (label, value) pairs
    /// </summary>
    public void UpdateData(List<(string Label, double Value)> data)
    {
        DataPoints.Clear();

        if (data == null || data.Count == 0)
        {
            IsEmpty = true;
            return;
        }

        IsEmpty = false;

        var maxValue = data.Max(d => Math.Abs(d.Value));
        if (maxValue <= 0) maxValue = 1;

        foreach (var (label, value) in data)
        {
            var normalizedHeight = Math.Abs(value) / maxValue * MaxChartHeight;
            normalizedHeight = Math.Max(4, normalizedHeight); // minimum visible height

            var color = value >= 0 ? "#F44336" : "#4CAF50"; // red for expense, green for income

            DataPoints.Add(new ChartDataPoint
            {
                Label = label,
                Value = value,
                BarHeight = normalizedHeight,
                BarColor = color,
            });
        }
    }

    /// <summary>
    /// Clear all chart data
    /// </summary>
    public void Clear()
    {
        DataPoints.Clear();
        IsEmpty = true;
    }
}
