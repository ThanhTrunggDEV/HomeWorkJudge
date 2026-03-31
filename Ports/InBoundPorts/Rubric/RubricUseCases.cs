using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Rubric;

namespace Ports.InBoundPorts.Rubric;

public interface IRubricUseCase
{
    Task<CreateRubricResult> CreateAsync(CreateRubricCommand command, CancellationToken ct = default);
    Task<GenerateRubricResult> GenerateByAiAsync(GenerateRubricCommand command, CancellationToken ct = default);
    Task AddCriteriaAsync(AddRubricCriteriaCommand command, CancellationToken ct = default);
    Task UpdateCriteriaAsync(UpdateRubricCriteriaCommand command, CancellationToken ct = default);
    Task RemoveCriteriaAsync(RemoveRubricCriteriaCommand command, CancellationToken ct = default);
    Task ReorderCriteriaAsync(ReorderRubricCriteriaCommand command, CancellationToken ct = default);
    Task<CloneRubricResult> CloneAsync(CloneRubricCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<RubricSummaryDto>> GetAllAsync(GetAllRubricsQuery query, CancellationToken ct = default);
    Task<RubricDetailDto> GetByIdAsync(Guid rubricId, CancellationToken ct = default);
    Task DeleteAsync(Guid rubricId, CancellationToken ct = default);
}
