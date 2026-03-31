using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        try
        {
            var query = new GetAllRubricsQuery(SearchKeyword);
            var list = await _rubricUseCase.GetAllAsync(query);
            Rubrics = new ObservableCollection<RubricSummaryDto>(list);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        await LoadAsync();
    }

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
        await _rubricUseCase.DeleteAsync(rubric.Id);
        Rubrics.Remove(rubric);
    }

    [RelayCommand]
    private async Task CloneAsync(RubricSummaryDto? rubric)
    {
        if (rubric is null) return;
        await _rubricUseCase.CloneAsync(new CloneRubricCommand(rubric.Id, $"{rubric.Name} (Copy)"));
        await LoadAsync();
    }

    [RelayCommand]
    private void ToggleAiPanel()
    {
        ShowAiPanel = !ShowAiPanel;
    }

    [RelayCommand]
    private async Task GenerateByAiAsync()
    {
        if (string.IsNullOrWhiteSpace(AiDescription)) return;
        var name = string.IsNullOrWhiteSpace(AiRubricName) ? "AI Generated Rubric" : AiRubricName;
        IsLoading = true;
        try
        {
            await _rubricUseCase.GenerateByAiAsync(new GenerateRubricCommand(AiDescription, name));
            AiDescription = "";
            AiRubricName = "";
            ShowAiPanel = false;
            await LoadAsync();
        }
        finally { IsLoading = false; }
    }
}
