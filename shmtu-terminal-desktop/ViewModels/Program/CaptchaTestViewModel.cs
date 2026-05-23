using System.Collections.ObjectModel;
using System.Reactive;
using ReactiveUI;
using shmtu.terminal.desktop.Services.Navigation;

namespace shmtu.terminal.desktop.ViewModels.Program;

/// <summary>
/// Captcha test history item for the DataGrid
/// </summary>
public class CaptchaTestResultItem
{
    public int TestNumber { get; set; }
    public string ResultText { get; set; } = "";
    public string ElapsedTime { get; set; } = "";
    public string Mode { get; set; } = "";
}

/// <summary>
/// ViewModel for the Captcha Test window
/// XAML bindings: CaptchaModes, SelectedCaptchaMode, CaptchaImage, HasCaptchaImage,
///   RefreshCaptchaCommand, RecognizeResult, RecognizeTime, RecognizeType,
///   TestRecognizeCommand, BatchTestCommand, IsBatchTesting, BatchProgress,
///   TestHistory, ClearHistoryCommand
/// </summary>
public class CaptchaTestViewModel : ViewModelBase
{
    public ObservableCollection<string> CaptchaModes { get; } = ["手动输入", "远程OCR", "本地ONNX"];

    private string _selectedCaptchaMode = "手动输入";
    public string SelectedCaptchaMode
    {
        get => _selectedCaptchaMode;
        set => this.RaiseAndSetIfChanged(ref _selectedCaptchaMode, value);
    }

    private Avalonia.Media.IImage? _captchaImage;
    public Avalonia.Media.IImage? CaptchaImage
    {
        get => _captchaImage;
        set
        {
            this.RaiseAndSetIfChanged(ref _captchaImage, value);
            this.RaisePropertyChanged(nameof(HasCaptchaImage));
        }
    }

    public bool HasCaptchaImage => _captchaImage != null;

    private string _recognizeResult = "";
    public string RecognizeResult
    {
        get => _recognizeResult;
        set => this.RaiseAndSetIfChanged(ref _recognizeResult, value);
    }

    private string _recognizeTime = "";
    public string RecognizeTime
    {
        get => _recognizeTime;
        set => this.RaiseAndSetIfChanged(ref _recognizeTime, value);
    }

    private string _recognizeType = "";
    public string RecognizeType
    {
        get => _recognizeType;
        set => this.RaiseAndSetIfChanged(ref _recognizeType, value);
    }

    private bool _isBatchTesting;
    public bool IsBatchTesting
    {
        get => _isBatchTesting;
        set => this.RaiseAndSetIfChanged(ref _isBatchTesting, value);
    }

    private double _batchProgress;
    public double BatchProgress
    {
        get => _batchProgress;
        set => this.RaiseAndSetIfChanged(ref _batchProgress, value);
    }

    public ObservableCollection<CaptchaTestResultItem> TestHistory { get; } = [];

    public ReactiveCommand<Unit, Unit> RefreshCaptchaCommand { get; }
    public ReactiveCommand<Unit, Unit> TestRecognizeCommand { get; }
    public ReactiveCommand<Unit, Unit> BatchTestCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearHistoryCommand { get; }

    private int _testCounter;

    public CaptchaTestViewModel()
    {
        RefreshCaptchaCommand = ReactiveCommand.CreateFromTask(RefreshCaptchaAsync);
        TestRecognizeCommand = ReactiveCommand.CreateFromTask(TestRecognizeAsync);
        BatchTestCommand = ReactiveCommand.CreateFromTask(BatchTestAsync);
        ClearHistoryCommand = ReactiveCommand.Create(() =>
        {
            TestHistory.Clear();
            _testCounter = 0;
        });
    }

    private async Task RefreshCaptchaAsync()
    {
        try
        {
            IsLoading = true;
            // TODO: Fetch captcha image from CAS server
            // For now, just clear the result
            RecognizeResult = "";
            RecognizeTime = "";
            RecognizeType = "";
        }
        catch (Exception ex)
        {
            ErrorMessage = $"刷新验证码失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task TestRecognizeAsync()
    {
        try
        {
            IsLoading = true;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            string result = "";
            string mode = SelectedCaptchaMode;

            // TODO: Use actual OCR service based on SelectedCaptchaMode
            // For now, just simulate
            await Task.Delay(100); // Simulate processing

            stopwatch.Stop();

            result = "(待实现)";
            RecognizeResult = result;
            RecognizeTime = $"{stopwatch.ElapsedMilliseconds}ms";
            RecognizeType = mode;

            _testCounter++;
            TestHistory.Insert(0, new CaptchaTestResultItem
            {
                TestNumber = _testCounter,
                ResultText = result,
                ElapsedTime = $"{stopwatch.ElapsedMilliseconds}ms",
                Mode = mode,
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"识别失败: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task BatchTestAsync()
    {
        IsBatchTesting = true;
        BatchProgress = 0;

        try
        {
            for (var i = 0; i < 10; i++)
            {
                await TestRecognizeAsync();
                BatchProgress = i + 1;
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"批量测试失败: {ex.Message}";
        }
        finally
        {
            IsBatchTesting = false;
        }
    }
}
