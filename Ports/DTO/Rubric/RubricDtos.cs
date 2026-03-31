using System;
using System.Collections.Generic;

namespace Ports.DTO.Rubric;

// ── Commands ──────────────────────────────────────────────────────────────────
public sealed record CreateRubricCommand(
    string Name,
    IReadOnlyList<RubricCriteriaInputDto> Criteria
);

public sealed record GenerateRubricCommand(
    string AssignmentDescription,
    string RubricName
);

public sealed record AddRubricCriteriaCommand(
    Guid RubricId,
    string Name,
    double MaxScore,
    string Description
);

public sealed record UpdateRubricCriteriaCommand(
    Guid RubricId,
    Guid CriteriaId,
    string Name,
    double MaxScore,
    string Description
);

public sealed record RemoveRubricCriteriaCommand(Guid RubricId, Guid CriteriaId);

public sealed record ReorderRubricCriteriaCommand(
    Guid RubricId,
    IReadOnlyList<Guid> OrderedCriteriaIds
);

public sealed record CloneRubricCommand(Guid SourceRubricId, string NewName);

// ── Results ───────────────────────────────────────────────────────────────────
public sealed record CreateRubricResult(Guid RubricId);
public sealed record GenerateRubricResult(Guid RubricId);
public sealed record CloneRubricResult(Guid NewRubricId);

// ── Query DTOs ────────────────────────────────────────────────────────────────
public sealed record GetAllRubricsQuery(string? SearchKeyword = null);

// ── Data DTOs (shared) ────────────────────────────────────────────────────────
public sealed record RubricCriteriaInputDto(
    string Name,
    double MaxScore,
    string Description
);

public sealed record RubricCriteriaDto(
    string Name,
    double MaxScore,
    string Description
);

public sealed record RubricScoreDto(
    string CriteriaName,
    double GivenScore,
    double MaxScore,
    string Comment
);

public sealed record RubricSummaryDto(
    Guid Id,
    string Name,
    double MaxTotalScore,
    int CriteriaCount,
    DateTime CreatedAt
);

public sealed record RubricDetailDto(
    Guid Id,
    string Name,
    DateTime CreatedAt,
    IReadOnlyList<RubricCriteriaDto> Criteria
);
