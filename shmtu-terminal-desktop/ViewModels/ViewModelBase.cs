using ReactiveUI;

namespace shmtu.terminal.desktop.ViewModels;

public class ViewModelBase : ReactiveObject
{
    /// <summary>
    /// Whether the ViewModel is currently loading data
    /// </summary>
    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    /// <summary>
    /// Error message to display, if any
    /// </summary>
    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }
}
