using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ports.OutBoundPorts.Storage;

public interface IFileStoragePort
{
    Task<string> UploadAsync(
        string fileName,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> DownloadAsync(string fileKey, CancellationToken cancellationToken = default);
}
