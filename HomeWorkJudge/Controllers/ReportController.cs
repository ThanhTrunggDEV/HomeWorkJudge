using System;
using System.Threading.Tasks;
using HomeWorkJudge.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ports.InBoundPorts.Query;
using Ports.InBoundPorts.Report;

namespace HomeWorkJudge.Controllers;

[Authorize(Policy = "TeacherOrAdmin")]
public sealed class ReportController : AppControllerBase
{
    private readonly ICheckClassroomAccessUseCase _checkClassroomAccessUseCase;
    private readonly IGetScoreboardUseCase _getScoreboardUseCase;
    private readonly IExportScoreReportUseCase _exportScoreReportUseCase;

    public ReportController(
        ICheckClassroomAccessUseCase checkClassroomAccessUseCase,
        IGetScoreboardUseCase getScoreboardUseCase,
        IExportScoreReportUseCase exportScoreReportUseCase)
    {
        _checkClassroomAccessUseCase = checkClassroomAccessUseCase;
        _getScoreboardUseCase = getScoreboardUseCase;
        _exportScoreReportUseCase = exportScoreReportUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> Scoreboard(Guid classroomId)
    {
        if (classroomId != Guid.Empty)
        {
            var accessResult = await EnsureCanAccessClassroomAsync(classroomId);
            if (accessResult is not null)
            {
                return accessResult;
            }
        }

        var items = classroomId == Guid.Empty
            ? Array.Empty<Ports.DTO.Report.ScoreboardItemDto>()
            : await _getScoreboardUseCase.HandleAsync(classroomId);

        return View(new ScoreboardViewModel
        {
            ClassroomId = classroomId,
            Items = items
        });
    }

    [Authorize(Policy = "TeacherOrAdmin")]
    [HttpGet]
    public async Task<IActionResult> Export(Guid classroomId, string format = "csv")
    {
        if (classroomId == Guid.Empty)
        {
            return BadRequest("ClassroomId is required.");
        }

        var accessResult = await EnsureCanAccessClassroomAsync(classroomId);
        if (accessResult is not null)
        {
            return accessResult;
        }

        var report = await _exportScoreReportUseCase.HandleAsync(classroomId, format);
        return File(report.Content, report.ContentType, report.FileName);
    }

    private async Task<IActionResult?> EnsureCanAccessClassroomAsync(Guid classroomId)
    {
        if (CurrentUserId is null)
        {
            return Challenge();
        }

        var decision = await _checkClassroomAccessUseCase.HandleAsync(
            classroomId,
            CurrentUserId.Value,
            CurrentUserRoleDto);

        return ToAccessActionResult(decision);
    }
}
