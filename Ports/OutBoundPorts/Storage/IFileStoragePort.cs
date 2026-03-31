using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Submission;

namespace Ports.OutBoundPorts.Storage;

/// <summary>
/// Outbound port: giải nén file zip/rar → danh sách SourceFile.
/// Chỉ trả về SOURCE CODE files:
///   ✅ Whitelist: .c, .cpp, .h, .hpp, .cs, .java, .py, .js, .ts, .go, .rs, .kt, .rb, .php, .html, .css, .txt, .md
///   ❌ Bỏ qua binary: .dll, .exe, .class, .o, .obj, .pyc
///   ❌ Bỏ qua artifacts: bin/, obj/, node_modules/, __pycache__/, .vs/
/// Throw nếu file không giải nén được hoặc không có file source code nào.
/// </summary>
public interface IFileExtractorPort
{
    Task<IReadOnlyList<SourceFileDto>> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
