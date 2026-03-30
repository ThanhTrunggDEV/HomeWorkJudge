using System;
using System.Threading.Tasks;
using Domain.Exception;
using HomeWorkJudge.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ports.DTO.Classroom;
using Ports.InBoundPorts.Classroom;
using Ports.InBoundPorts.Query;

namespace HomeWorkJudge.Controllers;

[Authorize]
public sealed class ClassroomController : AppControllerBase
{
    private readonly ICreateClassroomUseCase _createClassroomUseCase;
    private readonly IJoinClassroomUseCase _joinClassroomUseCase;
    private readonly IGetAuthorizedClassroomOverviewUseCase _getAuthorizedClassroomOverviewUseCase;

    public ClassroomController(
        ICreateClassroomUseCase createClassroomUseCase,
        IJoinClassroomUseCase joinClassroomUseCase,
        IGetAuthorizedClassroomOverviewUseCase getAuthorizedClassroomOverviewUseCase)
    {
        _createClassroomUseCase = createClassroomUseCase;
        _joinClassroomUseCase = joinClassroomUseCase;
        _getAuthorizedClassroomOverviewUseCase = getAuthorizedClassroomOverviewUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid? classroomId = null, string? joinCode = null)
    {
        var model = new ClassroomIndexViewModel
        {
            LastClassroomId = classroomId,
            LastJoinCode = joinCode
        };

        if (classroomId.HasValue && classroomId.Value != Guid.Empty && CurrentUserId is not null)
        {
            var response = await _getAuthorizedClassroomOverviewUseCase.HandleAsync(
                classroomId.Value,
                CurrentUserId.Value,
                CurrentUserRoleDto);

            var failure = ToAccessActionResult(response.AccessDecision);
            if (failure is not null)
            {
                return failure;
            }

            model.Overview = response.Classroom;
        }

        return View(model);
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "CreateForm")] CreateClassroomViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", new ClassroomIndexViewModel { CreateForm = model });
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var response = await _createClassroomUseCase.HandleAsync(
                new CreateClassroomRequestDto(model.Name.Trim(), CurrentUserId.Value));

            SetSuccess($"Created classroom successfully. Join code: {response.JoinCode}");
            return RedirectToAction(nameof(Index), new { classroomId = response.ClassroomId, joinCode = response.JoinCode });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", new ClassroomIndexViewModel { CreateForm = model });
        }
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join([Bind(Prefix = "JoinForm")] JoinClassroomViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", new ClassroomIndexViewModel { JoinForm = model });
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var response = await _joinClassroomUseCase.HandleAsync(
                new JoinClassroomRequestDto(model.JoinCode.Trim(), CurrentUserId.Value));

            SetSuccess($"Joined classroom successfully. ClassroomId: {response.ClassroomId}");
            return RedirectToAction(nameof(Index), new { classroomId = response.ClassroomId, joinCode = model.JoinCode.Trim() });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", new ClassroomIndexViewModel { JoinForm = model });
        }
    }
}
