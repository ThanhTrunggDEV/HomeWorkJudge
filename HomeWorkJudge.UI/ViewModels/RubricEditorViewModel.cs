using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ports.DTO.Rubric;
using Ports.InBoundPorts.Rubric;

namespace HomeWorkJudge.UI.ViewModels;

public partial class RubricEditorViewModel : ObservableObject
{
    private readonly IRubricUseCase _rubricUseCase;
    private readonly MainViewModel _mainVm;
    private Guid? _rubricId;

    [ObservableProperty] private string _rubricName = "";
    [ObservableProperty] private ObservableCollection<RubricCriteriaDto> _criteria = [];
    [ObservableProperty] private RubricCriteriaDto? _selectedCriteria;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isNewRubric;

    // Input fields for new criteria
    [ObservableProperty] private string _newCriteriaName = "";
    [ObservableProperty] private double _newCriteriaMaxScore = 2.0;
    [ObservableProperty] private string _newCriteriaDescription = "";

    public RubricEditorViewModel(IRubricUseCase rubricUseCase, MainViewModel mainVm, Guid? rubricId)
    {
        _rubricUseCase = rubricUseCase;
        _mainVm = mainVm;
        _rubricId = rubricId;
        IsNewRubric = rubricId is null;

        if (rubricId.HasValue)
            _ = LoadAsync(rubricId.Value);
    }

    private async Task LoadAsync(Guid id)
    {
        IsLoading = true;
        try
        {
            var detail = await _rubricUseCase.GetByIdAsync(id);
            RubricName = detail.Name;
            Criteria = new ObservableCollection<RubricCriteriaDto>(detail.Criteria);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(RubricName)) return;

        IsLoading = true;
        try
        {
            if (IsNewRubric)
            {
                var inputs = Criteria.Select(c => new RubricCriteriaInputDto(c.Name, c.MaxScore, c.Description)).ToList();
                var result = await _rubricUseCase.CreateAsync(new CreateRubricCommand(RubricName, inputs));
                _rubricId = result.RubricId;
                IsNewRubric = false;
            }
            // Navigate back
            _mainVm.NavigateTo("Rubrics");
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task AddCriteriaAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCriteriaName)) return;

        if (_rubricId.HasValue)
        {
            await _rubricUseCase.AddCriteriaAsync(new AddRubricCriteriaCommand(
                _rubricId.Value, NewCriteriaName, NewCriteriaMaxScore, NewCriteriaDescription));
            await LoadAsync(_rubricId.Value);
        }
        else
        {
            Criteria.Add(new RubricCriteriaDto(NewCriteriaName, NewCriteriaMaxScore, NewCriteriaDescription));
        }

        NewCriteriaName = "";
        NewCriteriaMaxScore = 2.0;
        NewCriteriaDescription = "";
    }

    [RelayCommand]
    private async Task RemoveCriteriaAsync(RubricCriteriaDto? criteria)
    {
        if (criteria is null) return;

        if (_rubricId.HasValue)
        {
            // Need criteria ID — for now remove from local list
            Criteria.Remove(criteria);
        }
        else
        {
            Criteria.Remove(criteria);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _mainVm.NavigateTo("Rubrics");
    }
}
