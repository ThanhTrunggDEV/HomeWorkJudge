using System.Threading;
using System.Threading.Tasks;

namespace Ports.OutBoundPorts.Plagiarism;

public interface IPlagiarismDetectionPort
{
    Task<double> CalculateSimilarityAsync(
        string leftSourceCode,
        string rightSourceCode,
        CancellationToken cancellationToken = default);
}
