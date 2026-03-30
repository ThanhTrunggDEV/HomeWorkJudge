using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Domain.Exception;
using HomeWorkJudge.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ports.DTO.Assignment;
using Ports.DTO.Common;
using Ports.DTO.Rubric;
using Ports.InBoundPorts.Assignment;

namespace HomeWorkJudge.Controllers;

[Authorize]
public sealed class AssignmentController : AppControllerBase
{
    private readonly IListAssignmentsUseCase _listAssignmentsUseCase;
    private readonly IGetAssignmentDetailUseCase _getAssignmentDetailUseCase;
    private readonly ICreateAssignmentUseCase _createAssignmentUseCase;
    private readonly IUpdateAssignmentUseCase _updateAssignmentUseCase;
    private readonly IPublishAssignmentUseCase _publishAssignmentUseCase;
    private readonly IAddAssignmentTestCaseUseCase _addAssignmentTestCaseUseCase;
    private readonly IUpdateAssignmentTestCaseUseCase _updateAssignmentTestCaseUseCase;
    private readonly IDeleteAssignmentTestCaseUseCase _deleteAssignmentTestCaseUseCase;
    private readonly IUpdateAssignmentRubricUseCase _updateAssignmentRubricUseCase;

    public AssignmentController(
        IListAssignmentsUseCase listAssignmentsUseCase,
        IGetAssignmentDetailUseCase getAssignmentDetailUseCase,
        ICreateAssignmentUseCase createAssignmentUseCase,
        IUpdateAssignmentUseCase updateAssignmentUseCase,
        IPublishAssignmentUseCase publishAssignmentUseCase,
        IAddAssignmentTestCaseUseCase addAssignmentTestCaseUseCase,
        IUpdateAssignmentTestCaseUseCase updateAssignmentTestCaseUseCase,
        IDeleteAssignmentTestCaseUseCase deleteAssignmentTestCaseUseCase,
        IUpdateAssignmentRubricUseCase updateAssignmentRubricUseCase)
    {
        _listAssignmentsUseCase = listAssignmentsUseCase;
        _getAssignmentDetailUseCase = getAssignmentDetailUseCase;
        _createAssignmentUseCase = createAssignmentUseCase;
        _updateAssignmentUseCase = updateAssignmentUseCase;
        _publishAssignmentUseCase = publishAssignmentUseCase;
        _addAssignmentTestCaseUseCase = addAssignmentTestCaseUseCase;
        _updateAssignmentTestCaseUseCase = updateAssignmentTestCaseUseCase;
        _deleteAssignmentTestCaseUseCase = deleteAssignmentTestCaseUseCase;
        _updateAssignmentRubricUseCase = updateAssignmentRubricUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid classroomId, int pageNumber = 1, int pageSize = 20)
    {
        if (classroomId == Guid.Empty)
        {
            SetError("ClassroomId is required.");
            return View(new AssignmentListPageViewModel());
        }

        var normalizedPageNumber = pageNumber < 1 ? 1 : pageNumber;
        var normalizedPageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var response = await _listAssignmentsUseCase.HandleAsync(
                classroomId,
                CurrentUserId.Value,
                new PagedRequestDto(normalizedPageNumber, normalizedPageSize));

            return View(new AssignmentListPageViewModel
            {
                ClassroomId = classroomId,
                PageNumber = response.PageNumber,
                PageSize = response.PageSize,
                TotalCount = response.TotalCount,
                Items = response.Items
            });
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
            return View(new AssignmentListPageViewModel
            {
                ClassroomId = classroomId,
                PageNumber = normalizedPageNumber,
                PageSize = normalizedPageSize
            });
        }
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpGet]
    public IActionResult Create(Guid classroomId)
    {
        if (classroomId == Guid.Empty)
        {
            return BadRequest("ClassroomId is required.");
        }

        return View(new CreateAssignmentViewModel
        {
            ClassroomId = classroomId,
            DueDate = DateTime.UtcNow.AddDays(7)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid assignmentId)
    {
        if (assignmentId == Guid.Empty)
        {
            return BadRequest("AssignmentId is required.");
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var assignment = await _getAssignmentDetailUseCase.HandleAsync(assignmentId, CurrentUserId.Value);
            return View(new AssignmentDetailPageViewModel
            {
                Assignment = assignment,
                AddTestCaseForm = new AddAssignmentTestCaseViewModel { AssignmentId = assignment.AssignmentId },
                RubricForm = new UpsertAssignmentRubricViewModel
                {
                    AssignmentId = assignment.AssignmentId,
                    CriteriaText = BuildRubricCriteriaText(assignment.RubricCriteria)
                }
            });
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound()
                : Forbid();
        }
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTestCase(AddAssignmentTestCaseViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetError("Invalid test case payload.");
            return RedirectToAction(nameof(Detail), new { assignmentId = model.AssignmentId });
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            await _addAssignmentTestCaseUseCase.HandleAsync(
                new AddAssignmentTestCaseRequestDto(
                    model.AssignmentId,
                    CurrentUserId.Value,
                    model.InputData,
                    model.ExpectedOutput,
                    model.IsHidden,
                    model.ScoreWeight));

            SetSuccess("Test case added.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Detail), new { assignmentId = model.AssignmentId });
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateTestCase(UpdateAssignmentTestCaseViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetError("Invalid test case update payload.");
            return RedirectToAction(nameof(Detail), new { assignmentId = model.AssignmentId });
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            await _updateAssignmentTestCaseUseCase.HandleAsync(
                new UpdateAssignmentTestCaseRequestDto(
                    model.AssignmentId,
                    CurrentUserId.Value,
                    model.TestCaseId,
                    model.InputData,
                    model.ExpectedOutput,
                    model.IsHidden,
                    model.ScoreWeight));

            SetSuccess("Test case updated.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Detail), new { assignmentId = model.AssignmentId });
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTestCase(DeleteAssignmentTestCaseViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetError("Invalid delete payload.");
            return RedirectToAction(nameof(Detail), new { assignmentId = model.AssignmentId });
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            await _deleteAssignmentTestCaseUseCase.HandleAsync(
                new DeleteAssignmentTestCaseRequestDto(model.AssignmentId, CurrentUserId.Value, model.TestCaseId));

            SetSuccess("Test case deleted.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Detail), new { assignmentId = model.AssignmentId });
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRubric(UpsertAssignmentRubricViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetError("Invalid rubric payload.");
            return RedirectToAction(nameof(Detail), new { assignmentId = model.AssignmentId });
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var criteria = ParseRubricCriteria(model.CriteriaText);
            await _updateAssignmentRubricUseCase.HandleAsync(
                new UpdateAssignmentRubricRequestDto(model.AssignmentId, CurrentUserId.Value, criteria));

            SetSuccess("Rubric updated.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }
        catch (FormatException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Detail), new { assignmentId = model.AssignmentId });
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid assignmentId)
    {
        if (assignmentId == Guid.Empty)
        {
            return BadRequest("AssignmentId is required.");
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var assignment = await _getAssignmentDetailUseCase.HandleAsync(assignmentId, CurrentUserId.Value);

            return View(new EditAssignmentViewModel
            {
                AssignmentId = assignment.AssignmentId,
                ClassroomId = assignment.ClassroomId,
                Title = assignment.Title,
                Description = assignment.Description,
                AllowedLanguagesCsv = string.Join(",", assignment.AllowedLanguages),
                DueDate = assignment.DueDate,
                TimeLimitMs = assignment.TimeLimitMs,
                MemoryLimitKb = assignment.MemoryLimitKb,
                MaxSubmissions = assignment.MaxSubmissions
            });
        }
        catch (DomainException ex)
        {
            return ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase)
                ? NotFound()
                : Forbid();
        }
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditAssignmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var languages = model.AllowedLanguagesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            await _updateAssignmentUseCase.HandleAsync(
                new UpdateAssignmentRequestDto(
                    model.AssignmentId,
                    CurrentUserId.Value,
                    model.Title.Trim(),
                    model.Description.Trim(),
                    model.DueDate,
                    languages,
                    model.TimeLimitMs,
                    model.MemoryLimitKb,
                    model.MaxSubmissions));

            SetSuccess("Assignment updated.");
            return RedirectToAction(nameof(Detail), new { assignmentId = model.AssignmentId });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateAssignmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var languages = model.AllowedLanguagesCsv
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var response = await _createAssignmentUseCase.HandleAsync(
                new CreateAssignmentRequestDto(
                    model.ClassroomId,
                    CurrentUserId.Value,
                    model.Title.Trim(),
                    model.Description.Trim(),
                    languages,
                    model.DueDate,
                    model.GradingType,
                    model.TimeLimitMs,
                    model.MemoryLimitKb,
                    model.MaxSubmissions));

            SetSuccess($"Assignment created with id {response.AssignmentId}.");
            return RedirectToAction(nameof(Index), new { classroomId = model.ClassroomId });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(PublishAssignmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            SetError("AssignmentId and ClassroomId are required.");
            return RedirectToAction(nameof(Index), new { classroomId = model.ClassroomId });
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            await _publishAssignmentUseCase.HandleAsync(new PublishAssignmentRequestDto(model.AssignmentId, CurrentUserId.Value));
            SetSuccess("Assignment published.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { classroomId = model.ClassroomId });
    }

    private static string BuildRubricCriteriaText(IReadOnlyList<RubricCriteriaDto> criteria)
    {
        if (criteria is null || criteria.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, criteria.Select(x => $"{x.Name}|{x.Description}|{x.Weight}"));
    }

    private static IReadOnlyList<RubricCriteriaDto> ParseRubricCriteria(string raw)
    {
        var lines = (raw ?? string.Empty)
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var criteria = new List<RubricCriteriaDto>(lines.Length);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var parts = line.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length != 3)
            {
                throw new FormatException($"Rubric line {index + 1} must follow 'Name|Description|Weight'.");
            }

            if (!double.TryParse(parts[2], out var weight) || weight <= 0)
            {
                throw new FormatException($"Rubric line {index + 1} has invalid weight.");
            }

            criteria.Add(new RubricCriteriaDto(parts[0], parts[1], weight));
        }

        if (criteria.Count == 0)
        {
            throw new FormatException("Rubric must include at least one criteria line.");
        }

        return criteria;
    }
}
