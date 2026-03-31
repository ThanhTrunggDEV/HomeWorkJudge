using System.Windows;
using HomeWorkJudge.UI.ViewModels;

namespace HomeWorkJudge.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
