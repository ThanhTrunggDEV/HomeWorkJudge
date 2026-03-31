using System.Collections.ObjectModel;
using Ports.DTO.Submission;

namespace HomeWorkJudge.UI.ViewModels;

/// <summary>
/// Node trong cây file — có thể là folder hoặc file.
/// </summary>
public class FileTreeNode
{
    public string Name { get; init; } = "";
    public bool IsFolder { get; init; }
    public SourceFileDto? File { get; init; }        // null nếu là folder
    public ObservableCollection<FileTreeNode> Children { get; } = [];

    // ── Factory ────────────────────────────────────────────────────────────

    /// <summary>
    /// Build cây từ danh sách SourceFileDto, phân cấp theo '/' trong FileName.
    /// </summary>
    public static ObservableCollection<FileTreeNode> Build(IEnumerable<SourceFileDto> files)
    {
        var root = new FileTreeNode { Name = "__root__", IsFolder = true };

        foreach (var file in files)
        {
            // Chuẩn hoá separator
            var parts = file.FileName
                .Replace('\\', '/')
                .Split('/', StringSplitOptions.RemoveEmptyEntries);

            InsertNode(root, parts, 0, file);
        }

        // Nếu chỉ có 1 folder root → bỏ qua node ảo, trả thẳng children
        return root.Children;
    }

    private static void InsertNode(FileTreeNode parent, string[] parts, int depth, SourceFileDto file)
    {
        if (depth == parts.Length - 1)
        {
            // Là file leaf
            parent.Children.Add(new FileTreeNode
            {
                Name = parts[depth],
                IsFolder = false,
                File = file
            });
            return;
        }

        // Folder segment
        var folderName = parts[depth];
        var existing = parent.Children.FirstOrDefault(n => n.IsFolder && n.Name == folderName);
        if (existing is null)
        {
            existing = new FileTreeNode { Name = folderName, IsFolder = true };
            parent.Children.Add(existing);
        }

        InsertNode(existing, parts, depth + 1, file);
    }
}
