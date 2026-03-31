using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Highlighting;
using HomeWorkJudge.UI.ViewModels;

namespace HomeWorkJudge.UI.Views;

public partial class SubmissionReviewView : UserControl
{
    public SubmissionReviewView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is SubmissionReviewViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(vm.CurrentFileContent))
                {
                    CodeEditor.Text = vm.CurrentFileContent;
                    ApplySyntaxHighlighting(vm.SelectedFile?.FileName);
                }
            };

            // Initial load
            CodeEditor.Text = vm.CurrentFileContent;
            ApplySyntaxHighlighting(vm.SelectedFile?.FileName);
        }
    }

    /// <summary>
    /// Xử lý chọn file trong TreeView — đáng tin cậy hơn MouseBinding.
    /// Chỉ chọn nếu là file node (không phải folder).
    /// </summary>
    private void FileTreeView_SelectedItemChanged(object sender,
        System.Windows.RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is not SubmissionReviewViewModel vm) return;
        if (e.NewValue is FileTreeNode node && !node.IsFolder)
        {
            vm.SelectFileNodeCommand.Execute(node);
        }
    }

    private void ApplySyntaxHighlighting(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            CodeEditor.SyntaxHighlighting = null;
            return;
        }

        var ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        var highlighting = ext switch
        {
            ".cs"                   => HighlightingManager.Instance.GetDefinition("C#"),
            ".java"                 => HighlightingManager.Instance.GetDefinition("Java"),
            ".py"                   => HighlightingManager.Instance.GetDefinition("Python"),
            ".js" or ".ts"          => HighlightingManager.Instance.GetDefinition("JavaScript"),
            ".c" or ".cpp" or ".h" or ".hpp" => HighlightingManager.Instance.GetDefinition("C++"),
            ".xml" or ".xaml"       => HighlightingManager.Instance.GetDefinition("XML"),
            ".html"                 => HighlightingManager.Instance.GetDefinition("HTML"),
            ".css"                  => HighlightingManager.Instance.GetDefinition("CSS"),
            ".php"                  => HighlightingManager.Instance.GetDefinition("PHP"),
            _                       => null
        };

        CodeEditor.SyntaxHighlighting = highlighting;
    }
}
