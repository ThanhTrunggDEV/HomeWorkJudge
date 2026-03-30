using System;
using System.Linq;
using System.Threading.Tasks;
using Domain.Exception;
using HomeWorkJudge.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ports.DTO.Assignment;
using Ports.DTO.Common;
using Ports.InBoundPorts.Assignment;

namespace HomeWorkJudge.Controllers;

[Authorize]
public sealed class AssignmentController : AppControllerBase
{
    private readonly IListAssignmentsUseCase _listAssignmentsUseCase;
    private readonly ICreateAssignmentUseCase _createAssignmentUseCase;
    private readonly IPublishAssignmentUseCase _publishAssignmentUseCase;

    public AssignmentController(
        IListAssignmentsUseCase listAssignmentsUseCase,
        ICreateAssignmentUseCase createAssignmentUseCase,
        IPublishAssignmentUseCase publishAssignmentUseCase)
    {
        _listAssignmentsUseCase = listAssignmentsUseCase;
        _createAssignmentUseCase = createAssignmentUseCase;
        _publishAssignmentUseCase = publishAssignmentUseCase;
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
}
