using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Exception;
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
    [ObservableProperty] private string? _errorMessage;

    // Form fields for add/edit criteria
    [ObservableProperty] private string _formCriteriaName = "";
    [ObservableProperty] private double _formCriteriaMaxScore = 2.0;
    [ObservableProperty] private string _formCriteriaDescription = "";

    // true = đang sửa tiêu chí đã chọn, false = đang thêm mới
    [ObservableProperty] private bool _isEditingCriteria;

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
        ErrorMessage = null;
        try
        {
            var detail = await _rubricUseCase.GetByIdAsync(id);
            RubricName = detail.Name;
            Criteria = new ObservableCollection<RubricCriteriaDto>(detail.Criteria);
        }
        catch (Exception ex) { ErrorMessage = $"Không thể tải rubric: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    // ── Chọn tiêu chí → đưa vào form sửa ──────────────────────────────────

    partial void OnSelectedCriteriaChanged(RubricCriteriaDto? value)
    {
        if (value is null) { ClearForm(); return; }
        FormCriteriaName = value.Name;
        FormCriteriaMaxScore = value.MaxScore;
        FormCriteriaDescription = value.Description;
        IsEditingCriteria = true;
    }

    // ── Save rubric ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(RubricName)) return;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            if (IsNewRubric)
            {
                var inputs = Criteria.Select(c => new RubricCriteriaInputDto(c.Name, c.MaxScore, c.Description)).ToList();
                var result = await _rubricUseCase.CreateAsync(new CreateRubricCommand(RubricName, inputs));
                _rubricId = result.RubricId;
                IsNewRubric = false;
            }
            _mainVm.NavigateTo("Rubrics");
        }
        catch (DomainException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex)       { ErrorMessage = $"Không thể lưu rubric: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    // ── Thêm tiêu chí ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task AddCriteriaAsync()
    {
        if (string.IsNullOrWhiteSpace(FormCriteriaName)) return;
        ErrorMessage = null;
        try
        {
            if (_rubricId.HasValue)
            {
                await _rubricUseCase.AddCriteriaAsync(new AddRubricCriteriaCommand(
                    _rubricId.Value, FormCriteriaName, FormCriteriaMaxScore, FormCriteriaDescription));
                await LoadAsync(_rubricId.Value);
            }
            else
            {
                Criteria.Add(new RubricCriteriaDto(Guid.NewGuid(), FormCriteriaName, FormCriteriaMaxScore, FormCriteriaDescription));
            }
            ClearForm();
        }
        catch (DomainException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex)       { ErrorMessage = $"Không thể thêm tiêu chí: {ex.Message}"; }
    }

    // ── Cập nhật tiêu chí đang chọn ─────────────────────────────────────────

    [RelayCommand]
    private async Task UpdateCriteriaAsync()
    {
        if (SelectedCriteria is null || string.IsNullOrWhiteSpace(FormCriteriaName)) return;
        ErrorMessage = null;
        try
        {
            if (_rubricId.HasValue)
            {
                await _rubricUseCase.UpdateCriteriaAsync(new UpdateRubricCriteriaCommand(
                    _rubricId.Value, SelectedCriteria.Id, FormCriteriaName, FormCriteriaMaxScore, FormCriteriaDescription));
                await LoadAsync(_rubricId.Value);
            }
            else
            {
                var idx = Criteria.IndexOf(SelectedCriteria);
                if (idx >= 0)
                    Criteria[idx] = new RubricCriteriaDto(SelectedCriteria.Id, FormCriteriaName, FormCriteriaMaxScore, FormCriteriaDescription);
            }
            ClearForm();
        }
        catch (DomainException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex)       { ErrorMessage = $"Không thể cập nhật tiêu chí: {ex.Message}"; }
    }

    // ── Xoá tiêu chí ────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task RemoveCriteriaAsync()
    {
        if (SelectedCriteria is null) return;
        ErrorMessage = null;
        try
        {
            if (_rubricId.HasValue)
            {
                await _rubricUseCase.RemoveCriteriaAsync(
                    new RemoveRubricCriteriaCommand(_rubricId.Value, SelectedCriteria.Id));
                await LoadAsync(_rubricId.Value);
            }
            else
            {
                Criteria.Remove(SelectedCriteria);
            }
            ClearForm();
        }
        catch (DomainException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex)       { ErrorMessage = $"Không thể xoá tiêu chí: {ex.Message}"; }
    }

    // ── Huỷ chỉnh sửa ──────────────────────────────────────────────────────

    [RelayCommand]
    private void CancelEdit()
    {
        SelectedCriteria = null;
        ClearForm();
    }

    [RelayCommand]
    private void GoBack() => _mainVm.NavigateTo("Rubrics");

    private void ClearForm()
    {
        FormCriteriaName = "";
        FormCriteriaMaxScore = 2.0;
        FormCriteriaDescription = "";
        IsEditingCriteria = false;
        SelectedCriteria = null;
    }
}
