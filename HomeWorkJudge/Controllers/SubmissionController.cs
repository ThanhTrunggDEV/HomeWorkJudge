using System;
using System.Threading.Tasks;
using Domain.Exception;
using HomeWorkJudge.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ports.DTO.Common;
using Ports.DTO.Submission;
using Ports.InBoundPorts.Query;
using Ports.InBoundPorts.Submission;

namespace HomeWorkJudge.Controllers;

[Authorize]
public sealed class SubmissionController : AppControllerBase
{
    private readonly ISubmitCodeUseCase _submitCodeUseCase;
    private readonly IGetAuthorizedSubmissionDetailUseCase _getAuthorizedSubmissionDetailUseCase;
    private readonly IGetSubmissionHistoryUseCase _getSubmissionHistoryUseCase;

    public SubmissionController(
        ISubmitCodeUseCase submitCodeUseCase,
        IGetAuthorizedSubmissionDetailUseCase getAuthorizedSubmissionDetailUseCase,
        IGetSubmissionHistoryUseCase getSubmissionHistoryUseCase)
    {
        _submitCodeUseCase = submitCodeUseCase;
        _getAuthorizedSubmissionDetailUseCase = getAuthorizedSubmissionDetailUseCase;
        _getSubmissionHistoryUseCase = getSubmissionHistoryUseCase;
    }

    [Authorize(Roles = "Student")]
    [HttpGet]
    public IActionResult Create(Guid assignmentId)
        => View(new SubmitCodeViewModel { AssignmentId = assignmentId });

    [Authorize(Roles = "Student")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SubmitCodeViewModel model)
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
            var response = await _submitCodeUseCase.HandleAsync(
                new SubmitCodeRequestDto(model.AssignmentId, CurrentUserId.Value, model.SourceCode, model.Language));

            SetSuccess($"Submission created with id {response.SubmissionId}.");
            return RedirectToAction(nameof(Detail), new { submissionId = response.SubmissionId });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Detail(Guid submissionId)
    {
        if (CurrentUserId is null)
        {
            return Challenge();
        }

        var authorizedDetail = await _getAuthorizedSubmissionDetailUseCase.HandleAsync(
            submissionId,
            CurrentUserId.Value,
            CurrentUserRoleDto);

        var failure = ToAccessActionResult(authorizedDetail.AccessDecision);
        if (failure is not null)
        {
            return failure;
        }

        return View(new SubmissionDetailPageViewModel
        {
            SubmissionId = submissionId,
            Submission = authorizedDetail.Submission
        });
    }

    [Authorize(Roles = "Student")]
    [HttpGet]
    public async Task<IActionResult> History(int pageNumber = 1, int pageSize = 20)
    {
        if (CurrentUserId is null)
        {
            return Challenge();
        }

        var page = await _getSubmissionHistoryUseCase.HandleAsync(
            CurrentUserId.Value,
            new PagedRequestDto(pageNumber, pageSize));

        return View(new SubmissionHistoryViewModel
        {
            StudentId = CurrentUserId.Value,
            Page = page
        });
    }
}
