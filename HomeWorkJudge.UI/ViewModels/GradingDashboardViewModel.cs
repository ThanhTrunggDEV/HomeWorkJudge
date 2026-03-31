using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ports.DTO.Grading;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Grading;
using Ports.InBoundPorts.GradingSession;
using Ports.InBoundPorts.Report;
using Ports.DTO.Report;

namespace HomeWorkJudge.UI.ViewModels;

public partial class GradingDashboardViewModel : ObservableObject
{
    private readonly IGradingUseCase _gradingUseCase;
    private readonly IGradingSessionUseCase _sessionUseCase;
    private readonly IReportUseCase _reportUseCase;
    private readonly MainViewModel _mainVm;
    private readonly IServiceProvider _sp;

    private Guid _sessionId;

    [ObservableProperty] private string _sessionName = "";
    [ObservableProperty] private ObservableCollection<SubmissionSummaryDto> _submissions = [];
    [ObservableProperty] private SubmissionSummaryDto? _selectedSubmission;
    [ObservableProperty] private SessionStatisticsDto? _statistics;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _statusMessage;

    // Plagiarism
    [ObservableProperty] private double _plagiarismThreshold = 70.0;
    [ObservableProperty] private ObservableCollection<PlagiarismResultDto> _plagiarismResults = [];

    public GradingDashboardViewModel(
        IGradingUseCase gradingUseCase,
        IGradingSessionUseCase sessionUseCase,
        IReportUseCase reportUseCase,
        MainViewModel mainVm,
        IServiceProvider sp)
    {
        _gradingUseCase = gradingUseCase;
        _sessionUseCase = sessionUseCase;
        _reportUseCase = reportUseCase;
        _mainVm = mainVm;
        _sp = sp;
    }

    public void Initialize(Guid sessionId, string sessionName)
    {
        _sessionId = sessionId;
        SessionName = sessionName;
        _ = RefreshAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var subs = await _gradingUseCase.GetSubmissionsBySessionAsync(_sessionId);
            Submissions = new ObservableCollection<SubmissionSummaryDto>(subs);
            Statistics = await _sessionUseCase.GetStatisticsAsync(_sessionId);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task StartGradingAsync()
    {
        IsLoading = true;
        StatusMessage = "Đang chấm bài...";
        try
        {
            var result = await _gradingUseCase.StartGradingAsync(new StartGradingCommand(_sessionId));
            StatusMessage = $"Đã chấm xong {result.StartedCount} bài.";
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"Lỗi: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RegradeAllAsync()
    {
        IsLoading = true;
        StatusMessage = "Đang chấm lại toàn bộ...";
        try
        {
            await _gradingUseCase.RegradeSessionAsync(new RegradeSessionCommand(_sessionId));
            StatusMessage = "Chấm lại xong.";
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"Lỗi: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RegradeOneAsync(SubmissionSummaryDto? sub)
    {
        if (sub is null) return;
        IsLoading = true;
        StatusMessage = $"Đang chấm lại {sub.StudentIdentifier}...";
        try
        {
            await _gradingUseCase.RegradeSubmissionAsync(new RegradeSubmissionCommand(sub.SubmissionId));
            StatusMessage = $"Đã chấm lại: {sub.StudentIdentifier}.";
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"Lỗi: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task CheckPlagiarismAsync()
    {
        IsLoading = true;
        StatusMessage = "Đang kiểm tra đạo văn...";
        try
        {
            var result = await _gradingUseCase.CheckPlagiarismAsync(
                new CheckPlagiarismCommand(_sessionId, PlagiarismThreshold));
            PlagiarismResults = new ObservableCollection<PlagiarismResultDto>(result.SuspectedPairs);
            StatusMessage = $"Phát hiện {result.SuspectedPairs.Count} cặp nghi ngờ.";
            await RefreshAsync();
        }
        catch (Exception ex) { StatusMessage = $"Lỗi: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ExportCsvAsync()
    {
        await ExportAsync(ExportFormat.Csv);
    }

    [RelayCommand]
    private async Task ExportExcelAsync()
    {
        await ExportAsync(ExportFormat.Excel);
    }

    private async Task ExportAsync(ExportFormat format)
    {
        try
        {
            var result = await _reportUseCase.ExportAsync(
                new ExportScoreCommand(_sessionId, format, false));

            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = result.FileName,
                Filter = format == ExportFormat.Csv ? "CSV|*.csv" : "Excel|*.xlsx"
            };
            if (dlg.ShowDialog() == true)
            {
                await System.IO.File.WriteAllBytesAsync(dlg.FileName, result.FileBytes);
                StatusMessage = $"Đã xuất: {dlg.FileName}";
            }
        }
        catch (Exception ex) { StatusMessage = $"Lỗi export: {ex.Message}"; }
    }

    [RelayCommand]
    private void OpenSubmission(SubmissionSummaryDto? sub)
    {
        if (sub is null) return;
        var vm = _sp.GetService(typeof(SubmissionReviewViewModel)) as SubmissionReviewViewModel;
        if (vm is not null)
        {
            var allIds = Submissions.Select(s => s.SubmissionId).ToList();
            vm.Initialize(sub.SubmissionId, allIds, _mainVm, this);
            _mainVm.NavigateToViewModel(vm);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _mainVm.NavigateTo("Sessions");
    }
}
