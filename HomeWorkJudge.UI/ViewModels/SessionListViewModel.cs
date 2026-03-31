using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ports.DTO.GradingSession;
using Ports.InBoundPorts.GradingSession;

namespace HomeWorkJudge.UI.ViewModels;

public partial class SessionListViewModel : ObservableObject
{
    private readonly IGradingSessionUseCase _sessionUseCase;
    private readonly MainViewModel _mainVm;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private ObservableCollection<GradingSessionSummaryDto> _sessions = [];
    [ObservableProperty] private GradingSessionSummaryDto? _selectedSession;
    [ObservableProperty] private bool _isLoading;

    public SessionListViewModel(IGradingSessionUseCase sessionUseCase, MainViewModel mainVm, IServiceProvider sp)
    {
        _sessionUseCase = sessionUseCase;
        _mainVm = mainVm;
        _sp = sp;
        _ = LoadAsync();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var list = await _sessionUseCase.GetAllAsync();
            Sessions = new ObservableCollection<GradingSessionSummaryDto>(list);
        }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private void CreateNew()
    {
        var vm = _sp.GetService(typeof(SessionCreateViewModel)) as SessionCreateViewModel;
        if (vm is not null) _mainVm.NavigateToViewModel(vm);
    }

    [RelayCommand]
    private void OpenSession(GradingSessionSummaryDto? session)
    {
        if (session is null) return;
        var vm = _sp.GetService(typeof(GradingDashboardViewModel)) as GradingDashboardViewModel;
        if (vm is not null)
        {
            vm.Initialize(session.SessionId, session.Name);
            _mainVm.NavigateToViewModel(vm);
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(GradingSessionSummaryDto? session)
    {
        if (session is null) return;
        await _sessionUseCase.DeleteAsync(session.SessionId);
        Sessions.Remove(session);
    }
}
