using System.Collections.ObjectModel;
using System.Net.Http;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Exception;
using Ports.DTO.Rubric;
using Ports.InBoundPorts.Rubric;

namespace HomeWorkJudge.UI.ViewModels;

public partial class RubricListViewModel : ObservableObject
{
    private readonly IRubricUseCase _rubricUseCase;
    private readonly MainViewModel _mainVm;

    [ObservableProperty] private ObservableCollection<RubricSummaryDto> _rubrics = [];
    [ObservableProperty] private string? _searchKeyword;
    [ObservableProperty] private RubricSummaryDto? _selectedRubric;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string? _errorMessage;

    // AI Generation inputs
    [ObservableProperty] private string _aiRubricName = "";
    [ObservableProperty] private string _aiDescription = "";
    [ObservableProperty] private bool _showAiPanel;

    public RubricListViewModel(IRubricUseCase rubricUseCase, MainViewModel mainVm)
    {
        _rubricUseCase = rubricUseCase;
        _mainVm = mainVm;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var query = new GetAllRubricsQuery(SearchKeyword);
            var list = await _rubricUseCase.GetAllAsync(query);
            Rubrics = new ObservableCollection<RubricSummaryDto>(list);
        }
        catch (Exception ex) { ErrorMessage = $"Không thể tải danh sách rubric: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SearchAsync() => await LoadAsync();

    [RelayCommand]
    private void CreateNew()
    {
        var editorVm = new RubricEditorViewModel(_rubricUseCase, _mainVm, rubricId: null);
        _mainVm.NavigateToViewModel(editorVm);
    }

    [RelayCommand]
    private void EditRubric(RubricSummaryDto? rubric)
    {
        if (rubric is null) return;
        var editorVm = new RubricEditorViewModel(_rubricUseCase, _mainVm, rubric.Id);
        _mainVm.NavigateToViewModel(editorVm);
    }

    [RelayCommand]
    private async Task DeleteAsync(RubricSummaryDto? rubric)
    {
        if (rubric is null) return;
        ErrorMessage = null;
        try
        {
            await _rubricUseCase.DeleteAsync(rubric.Id);
            Rubrics.Remove(rubric);
        }
        catch (DomainException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex) { ErrorMessage = $"Không thể xoá rubric: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task CloneAsync(RubricSummaryDto? rubric)
    {
        if (rubric is null) return;
        ErrorMessage = null;
        try
        {
            await _rubricUseCase.CloneAsync(new CloneRubricCommand(rubric.Id, $"{rubric.Name} (Copy)"));
            await LoadAsync();
        }
        catch (Exception ex) { ErrorMessage = $"Không thể nhân bản rubric: {ex.Message}"; }
    }

    [RelayCommand]
    private void ToggleAiPanel() => ShowAiPanel = !ShowAiPanel;

    [RelayCommand]
    private async Task GenerateByAiAsync()
    {
        if (string.IsNullOrWhiteSpace(AiDescription)) return;
        var name = string.IsNullOrWhiteSpace(AiRubricName) ? "AI Generated Rubric" : AiRubricName;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            await _rubricUseCase.GenerateByAiAsync(new GenerateRubricCommand(AiDescription, name));
            AiDescription = "";
            AiRubricName = "";
            ShowAiPanel = false;
            await LoadAsync();
        }
        catch (HttpRequestException) { ErrorMessage = "Không thể kết nối AI. Kiểm tra API key và kết nối mạng."; }
        catch (TaskCanceledException)  { ErrorMessage = "Yêu cầu AI bị hết thời gian. Vui lòng thử lại."; }
        catch (Exception ex)           { ErrorMessage = $"Lỗi tạo rubric AI: {ex.Message}"; }
        finally { IsLoading = false; }
    }
}
