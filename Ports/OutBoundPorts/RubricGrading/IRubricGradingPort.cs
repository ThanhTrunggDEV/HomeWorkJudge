using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Rubric;

namespace Ports.OutBoundPorts.RubricGrading;

public interface IRubricGradingPort
{
    Task<IReadOnlyList<RubricScoreDto>> GradeByRubricAsync(
        string sourceCode,
        IReadOnlyList<RubricCriteriaDto> criteria,
        CancellationToken cancellationToken = default);
}
