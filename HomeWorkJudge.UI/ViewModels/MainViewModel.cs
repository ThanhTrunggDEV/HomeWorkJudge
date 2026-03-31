using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HomeWorkJudge.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentView))]
    private string _currentPage = "Rubrics";

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private bool _isSidebarCollapsed;

    [RelayCommand]
    private void ToggleSidebar() => IsSidebarCollapsed = !IsSidebarCollapsed;

    public MainViewModel(IServiceProvider sp)
    {
        _sp = sp;
    }

    public void Initialize()
    {
        if (CurrentView is null)
            NavigateTo(CurrentPage);
    }

    [RelayCommand]
    public void NavigateTo(string page)
    {
        CurrentPage = page;
        CurrentView = page switch
        {
            "Rubrics"  => Resolve<RubricListViewModel>(),
            "Sessions" => Resolve<SessionListViewModel>(),
            "Settings" => Resolve<SettingsViewModel>(),
            _ => CurrentView
        };
    }

    // Cho phép navigation từ child VM (VD: click rubric → editor)
    public void NavigateToViewModel(ObservableObject vm)
    {
        CurrentView = vm;
    }

    private T Resolve<T>() where T : notnull
        => (T)_sp.GetService(typeof(T))!;
}
