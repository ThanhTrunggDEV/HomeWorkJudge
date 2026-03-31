using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entity;
using Domain.Exception;
using Domain.Ports;
using Domain.ValueObject;
using Ports.DTO.Rubric;
using Ports.InBoundPorts.Rubric;
using Ports.OutBoundPorts.AI;

namespace Application.UseCases;

public sealed class RubricUseCaseHandler : IRubricUseCase
{
    private readonly IRubricRepository _rubricRepo;
    private readonly IUnitOfWork _uow;
    private readonly IAiRubricGeneratorPort _aiGenerator;

    public RubricUseCaseHandler(
        IRubricRepository rubricRepo,
        IUnitOfWork uow,
        IAiRubricGeneratorPort aiGenerator)
    {
        _rubricRepo = rubricRepo;
        _uow = uow;
        _aiGenerator = aiGenerator;
    }

    // UC-01: Tạo rubric thủ công
    public async Task<CreateRubricResult> CreateAsync(CreateRubricCommand command, CancellationToken ct = default)
    {
        var rubric = new Rubric(new RubricId(Guid.NewGuid()), command.Name);

        foreach (var c in command.Criteria)
            rubric.AddCriteria(c.Name, c.MaxScore, c.Description);

        await _rubricRepo.AddAsync(rubric, ct);
        await _uow.SaveChangesAsync(ct);

        return new CreateRubricResult(rubric.Id.Value);
    }

    // UC-02: AI tạo rubric gợi ý
    public async Task<GenerateRubricResult> GenerateByAiAsync(GenerateRubricCommand command, CancellationToken ct = default)
    {
        var criteriaDto = await _aiGenerator.GenerateAsync(command.AssignmentDescription, ct);

        var rubric = new Rubric(new RubricId(Guid.NewGuid()), command.RubricName);
        foreach (var c in criteriaDto)
            rubric.AddCriteria(c.Name, c.MaxScore, c.Description);

        await _rubricRepo.AddAsync(rubric, ct);
        await _uow.SaveChangesAsync(ct);

        return new GenerateRubricResult(rubric.Id.Value);
    }

    // UC-03: Thêm tiêu chí
    public async Task AddCriteriaAsync(AddRubricCriteriaCommand command, CancellationToken ct = default)
    {
        var rubric = await GetOrThrowAsync(command.RubricId, ct);
        rubric.AddCriteria(command.Name, command.MaxScore, command.Description);
        await _rubricRepo.UpdateAsync(rubric, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // UC-03: Sửa tiêu chí
    public async Task UpdateCriteriaAsync(UpdateRubricCriteriaCommand command, CancellationToken ct = default)
    {
        var rubric = await GetOrThrowAsync(command.RubricId, ct);
        rubric.UpdateCriteria(new RubricCriteriaId(command.CriteriaId), command.Name, command.MaxScore, command.Description);
        await _rubricRepo.UpdateAsync(rubric, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // UC-03: Xoá tiêu chí
    public async Task RemoveCriteriaAsync(RemoveRubricCriteriaCommand command, CancellationToken ct = default)
    {
        var rubric = await GetOrThrowAsync(command.RubricId, ct);
        rubric.RemoveCriteria(new RubricCriteriaId(command.CriteriaId));
        await _rubricRepo.UpdateAsync(rubric, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // UC-03: Sắp xếp lại thứ tự tiêu chí
    public async Task ReorderCriteriaAsync(ReorderRubricCriteriaCommand command, CancellationToken ct = default)
    {
        var rubric = await GetOrThrowAsync(command.RubricId, ct);
        rubric.ReorderCriteria(command.OrderedCriteriaIds.Select(id => new RubricCriteriaId(id)).ToList());
        await _rubricRepo.UpdateAsync(rubric, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // UC-03: Đổi tên rubric
    public async Task UpdateNameAsync(UpdateRubricNameCommand command, CancellationToken ct = default)
    {
        var rubric = await GetOrThrowAsync(command.RubricId, ct);
        rubric.Rename(command.NewName);
        await _rubricRepo.UpdateAsync(rubric, ct);
        await _uow.SaveChangesAsync(ct);
    }

    // UC-03: Nhân bản rubric
    public async Task<CloneRubricResult> CloneAsync(CloneRubricCommand command, CancellationToken ct = default)
    {
        var source = await GetOrThrowAsync(command.SourceRubricId, ct);
        var clone = source.Clone(command.NewName);
        await _rubricRepo.AddAsync(clone, ct);
        await _uow.SaveChangesAsync(ct);
        return new CloneRubricResult(clone.Id.Value);
    }

    // UC-04: Xem danh sách rubric
    public async Task<IReadOnlyList<RubricSummaryDto>> GetAllAsync(GetAllRubricsQuery query, CancellationToken ct = default)
    {
        var rubrics = string.IsNullOrWhiteSpace(query.SearchKeyword)
            ? await _rubricRepo.GetAllAsync(ct)
            : await _rubricRepo.SearchByNameAsync(query.SearchKeyword, ct);

        return rubrics.Select(r => new RubricSummaryDto(
            Id: r.Id.Value,
            Name: r.Name,
            MaxTotalScore: r.GetMaxTotalScore(),
            CriteriaCount: r.Criteria.Count,
            CreatedAt: r.CreatedAt
        )).ToList();
    }

    // UC-04: Xem chi tiết rubric
    public async Task<RubricDetailDto> GetByIdAsync(Guid rubricId, CancellationToken ct = default)
    {
        var rubric = await GetOrThrowAsync(rubricId, ct);
        return new RubricDetailDto(
            Id: rubric.Id.Value,
            Name: rubric.Name,
            CreatedAt: rubric.CreatedAt,
            Criteria: rubric.Criteria.Select(c => new RubricCriteriaDto(c.Id.Value, c.Name, c.MaxScore, c.Description)).ToList()
        );
    }

    // UC-04: Xoá rubric
    public async Task DeleteAsync(Guid rubricId, CancellationToken ct = default)
    {
        await _rubricRepo.DeleteAsync(new RubricId(rubricId), ct);
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Rubric> GetOrThrowAsync(Guid rubricId, CancellationToken ct)
        => await _rubricRepo.GetByIdAsync(new RubricId(rubricId), ct)
           ?? throw new DomainException($"Không tìm thấy Rubric Id={rubricId}.");
}
