using System;
using System.Threading.Tasks;
using Domain.Exception;
using HomeWorkJudge.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ports.DTO.Classroom;
using Ports.InBoundPorts.Classroom;

namespace HomeWorkJudge.Controllers;

[Authorize]
public sealed class ClassroomController : AppControllerBase
{
    private readonly ICreateClassroomUseCase _createClassroomUseCase;
    private readonly IJoinClassroomUseCase _joinClassroomUseCase;

    public ClassroomController(
        ICreateClassroomUseCase createClassroomUseCase,
        IJoinClassroomUseCase joinClassroomUseCase)
    {
        _createClassroomUseCase = createClassroomUseCase;
        _joinClassroomUseCase = joinClassroomUseCase;
    }

    [HttpGet]
    public IActionResult Index(Guid? classroomId = null, string? joinCode = null)
    {
        return View(new ClassroomIndexViewModel
        {
            LastClassroomId = classroomId,
            LastJoinCode = joinCode
        });
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClassroomIndexViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var response = await _createClassroomUseCase.HandleAsync(
                new CreateClassroomRequestDto(model.CreateForm.Name, CurrentUserId.Value));

            SetSuccess($"Created classroom successfully. Join code: {response.JoinCode}");
            return RedirectToAction(nameof(Index), new { classroomId = response.ClassroomId, joinCode = response.JoinCode });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", model);
        }
    }

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(ClassroomIndexViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        if (CurrentUserId is null)
        {
            return Challenge();
        }

        try
        {
            var response = await _joinClassroomUseCase.HandleAsync(
                new JoinClassroomRequestDto(model.JoinForm.JoinCode, CurrentUserId.Value));

            SetSuccess($"Joined classroom successfully. ClassroomId: {response.ClassroomId}");
            return RedirectToAction(nameof(Index), new { classroomId = response.ClassroomId, joinCode = model.JoinForm.JoinCode });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Index", model);
        }
    }
}
