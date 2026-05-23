using ReactiveUI;

namespace shmtu.terminal.desktop.ViewModels.Component;

/// <summary>
/// ViewModel for the summary card component
/// XAML bindings: Title, Value, ValueColor, Subtitle
/// </summary>
public class SummaryCardViewModel : ViewModelBase
{
    private string _title = "";
    public string Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    private string _value = "0.00";
    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }

    private string _valueColor = "#1976D2";
    public string ValueColor
    {
        get => _valueColor;
        set => this.RaiseAndSetIfChanged(ref _valueColor, value);
    }

    private string _subtitle = "";
    public string Subtitle
    {
        get => _subtitle;
        set => this.RaiseAndSetIfChanged(ref _subtitle, value);
    }

    public SummaryCardViewModel() { }

    public SummaryCardViewModel(string title, string value, string valueColor, string subtitle)
    {
        _title = title;
        _value = value;
        _valueColor = valueColor;
        _subtitle = subtitle;
    }
}
