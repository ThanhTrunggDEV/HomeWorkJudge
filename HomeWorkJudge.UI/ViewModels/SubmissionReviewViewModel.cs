using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ports.DTO.Grading;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Grading;

namespace HomeWorkJudge.UI.ViewModels;

public partial class SubmissionReviewViewModel : ObservableObject
{
    private readonly IGradingUseCase _gradingUseCase;
    private MainViewModel? _mainVm;
    private GradingDashboardViewModel? _dashboardVm;

    private Guid _submissionId;
    private List<Guid> _allSubmissionIds = [];
    private int _currentIndex;

    [ObservableProperty] private string _studentIdentifier = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private double _totalScore;
    [ObservableProperty] private string? _teacherNote;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isPlagiarismSuspected;

    [ObservableProperty] private ObservableCollection<SourceFileDto> _sourceFiles = [];
    [ObservableProperty] private SourceFileDto? _selectedFile;
    [ObservableProperty] private string _currentFileContent = "";
    [ObservableProperty] private ObservableCollection<FileTreeNode> _fileTree = [];
    [ObservableProperty] private bool _isFileTreeVisible = true;

    [RelayCommand]
    private void ToggleFileTree() => IsFileTreeVisible = !IsFileTreeVisible;

    [ObservableProperty] private ObservableCollection<RubricResultDto> _rubricResults = [];
    [ObservableProperty] private bool _isLoading;

    // Override fields
    [ObservableProperty] private string _overrideCriteriaName = "";
    [ObservableProperty] private double _overrideScore;
    [ObservableProperty] private string _overrideComment = "";
    [ObservableProperty] private double _overrideTotalScore;

    public bool HasPrev => _currentIndex > 0;
    public bool HasNext => _currentIndex < _allSubmissionIds.Count - 1;
    public string NavigationInfo => $"{_currentIndex + 1} / {_allSubmissionIds.Count}";
    public bool CanApprove => Status == "AIGraded";
    public bool CanOverride => Status is "AIGraded" or "Reviewed";

    partial void OnStatusChanged(string value)
    {
        OnPropertyChanged(nameof(CanApprove));
        OnPropertyChanged(nameof(CanOverride));
    }

    public SubmissionReviewViewModel(IGradingUseCase gradingUseCase)
    {
        _gradingUseCase = gradingUseCase;
    }

    public void Initialize(Guid submissionId, List<Guid> allIds, MainViewModel mainVm, GradingDashboardViewModel dashboardVm)
    {
        _submissionId = submissionId;
        _allSubmissionIds = allIds;
        _currentIndex = allIds.IndexOf(submissionId);
        _mainVm = mainVm;
        _dashboardVm = dashboardVm;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var detail = await _gradingUseCase.GetSubmissionDetailAsync(_submissionId);
            StudentIdentifier = detail.StudentIdentifier;
            Status = detail.Status;
            TotalScore = detail.TotalScore;
            TeacherNote = detail.TeacherNote;
            ErrorMessage = detail.ErrorMessage;
            IsPlagiarismSuspected = detail.IsPlagiarismSuspected;
            SourceFiles = new ObservableCollection<SourceFileDto>(detail.SourceFiles);
            FileTree = FileTreeNode.Build(detail.SourceFiles);
            RubricResults = new ObservableCollection<RubricResultDto>(detail.RubricResults);
            OverrideTotalScore = detail.TotalScore;

            // Chọn file đầu tiên (leaf đầu tiên trong cây)
            var firstFile = detail.SourceFiles.FirstOrDefault();
            if (firstFile is not null)
            {
                SelectedFile = firstFile;
                CurrentFileContent = firstFile.Content;
            }

            OnPropertyChanged(nameof(HasPrev));
            OnPropertyChanged(nameof(HasNext));
            OnPropertyChanged(nameof(NavigationInfo));
        }
        finally { IsLoading = false; }
    }

    partial void OnSelectedFileChanged(SourceFileDto? value)
    {
        CurrentFileContent = value?.Content ?? "";
    }

    /// <summary>Được gọi khi user click vào file node trong TreeView.</summary>
    [RelayCommand]
    private void SelectFileNode(FileTreeNode? node)
    {
        if (node is null || node.IsFolder || node.File is null) return;
        SelectedFile = node.File;
        CurrentFileContent = node.File.Content;
    }

    [RelayCommand(CanExecute = nameof(CanApprove))]
    private async Task ApproveAsync()
    {
        await _gradingUseCase.ApproveAsync(new ApproveSubmissionCommand(_submissionId));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OverrideCriteriaAsync()
    {
        if (string.IsNullOrWhiteSpace(OverrideCriteriaName)) return;
        await _gradingUseCase.OverrideCriteriaScoreAsync(
            new OverrideCriteriaScoreCommand(_submissionId, OverrideCriteriaName, OverrideScore, OverrideComment));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OverrideTotalAsync()
    {
        await _gradingUseCase.OverrideTotalScoreAsync(
            new OverrideTotalScoreCommand(_submissionId, OverrideTotalScore));
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SaveTeacherNoteAsync()
    {
        await _gradingUseCase.AddTeacherNoteAsync(
            new AddTeacherNoteCommand(_submissionId, TeacherNote));
    }

    [RelayCommand]
    private async Task RegradeAsync()
    {
        IsLoading = true;
        try
        {
            await _gradingUseCase.RegradeSubmissionAsync(new RegradeSubmissionCommand(_submissionId));
            await LoadAsync();
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task GoNextAsync()
    {
        if (!HasNext) return;
        _currentIndex++;
        _submissionId = _allSubmissionIds[_currentIndex];
        await LoadAsync();
    }

    [RelayCommand]
    private async Task GoPrevAsync()
    {
        if (!HasPrev) return;
        _currentIndex--;
        _submissionId = _allSubmissionIds[_currentIndex];
        await LoadAsync();
    }

    [RelayCommand]
    private void GoBack()
    {
        if (_dashboardVm is not null)
        {
            _ = _dashboardVm.RefreshAsync();
            _mainVm?.NavigateToViewModel(_dashboardVm);
        }
    }
}
