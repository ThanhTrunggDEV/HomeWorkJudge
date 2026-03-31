using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Ports.DTO.GradingSession;
using Ports.DTO.Rubric;
using Ports.InBoundPorts.GradingSession;
using Ports.InBoundPorts.Rubric;

namespace HomeWorkJudge.UI.ViewModels;

public partial class SessionCreateViewModel : ObservableObject
{
    private readonly IGradingSessionUseCase _sessionUseCase;
    private readonly IRubricUseCase _rubricUseCase;
    private readonly MainViewModel _mainVm;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private string _sessionName = "";
    [ObservableProperty] private ObservableCollection<RubricSummaryDto> _rubrics = [];
    [ObservableProperty] private RubricSummaryDto? _selectedRubric;
    [ObservableProperty] private ObservableCollection<string> _selectedFiles = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _resultMessage;

    public SessionCreateViewModel(
        IGradingSessionUseCase sessionUseCase,
        IRubricUseCase rubricUseCase,
        MainViewModel mainVm,
        IServiceProvider sp)
    {
        _sessionUseCase = sessionUseCase;
        _rubricUseCase = rubricUseCase;
        _mainVm = mainVm;
        _sp = sp;
        _ = LoadRubricsAsync();
    }

    private async Task LoadRubricsAsync()
    {
        var list = await _rubricUseCase.GetAllAsync(new GetAllRubricsQuery());
        Rubrics = new ObservableCollection<RubricSummaryDto>(list);
    }

    [RelayCommand]
    private void SelectFiles()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Chọn file bài nộp (zip/rar)",
            Filter = "Archive files|*.zip;*.rar;*.7z|All files|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog() == true)
        {
            SelectedFiles = new ObservableCollection<string>(dlg.FileNames);
        }
    }

    [RelayCommand]
    private async Task CreateSessionAsync()
    {
        if (string.IsNullOrWhiteSpace(SessionName) || SelectedRubric is null || SelectedFiles.Count == 0)
            return;

        IsLoading = true;
        try
        {
            var command = new CreateSessionCommand(
                SessionName.Trim(),
                SelectedRubric.Id,
                SelectedFiles.ToList());

            var result = await _sessionUseCase.CreateAsync(command);

            // Chuyển hướng đến Grading Dashboard
            var dashboardVm = _sp.GetService(typeof(GradingDashboardViewModel)) as GradingDashboardViewModel;
            if (dashboardVm is not null)
            {
                dashboardVm.Initialize(result.SessionId, SessionName.Trim());
                _mainVm.NavigateToViewModel(dashboardVm);
            }
        }
        catch (Exception ex)
        {
            ResultMessage = $"Lỗi: {ex.Message}";
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void GoBack()
    {
        _mainVm.NavigateTo("Sessions");
    }
}
