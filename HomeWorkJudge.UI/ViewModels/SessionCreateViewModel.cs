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

    [ObservableProperty] private string _sessionName = "";
    [ObservableProperty] private ObservableCollection<RubricSummaryDto> _rubrics = [];
    [ObservableProperty] private RubricSummaryDto? _selectedRubric;
    [ObservableProperty] private ObservableCollection<string> _selectedFiles = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _resultMessage;

    public SessionCreateViewModel(
        IGradingSessionUseCase sessionUseCase,
        IRubricUseCase rubricUseCase,
        MainViewModel mainVm)
    {
        _sessionUseCase = sessionUseCase;
        _rubricUseCase = rubricUseCase;
        _mainVm = mainVm;
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
            ResultMessage = $"Tạo phiên thành công! Import: {result.ImportedCount} bài, bỏ qua: {result.SkippedCount} file.";
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
