using System;
using System.Threading.Tasks;
using Domain.Exception;
using HomeWorkJudge.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Ports.DTO.AI;
using Ports.DTO.Rubric;
using Ports.InBoundPorts.Grading;
using Ports.InBoundPorts.Query;

namespace HomeWorkJudge.Controllers;

[Authorize(Policy = "TeacherOrAdmin")]
public sealed class GradingController : AppControllerBase
{
    private readonly IGradeSubmissionByTestCaseUseCase _gradeSubmissionByTestCaseUseCase;
    private readonly IGradeSubmissionByRubricUseCase _gradeSubmissionByRubricUseCase;
    private readonly IOverrideSubmissionScoreUseCase _overrideSubmissionScoreUseCase;
    private readonly IReviewAiRubricResultUseCase _reviewAiRubricResultUseCase;
    private readonly IOverrideRubricCriteriaScoreUseCase _overrideRubricCriteriaScoreUseCase;
    private readonly IExplainRubricDeductionUseCase _explainRubricDeductionUseCase;
    private readonly IGetAuthorizedSubmissionDetailUseCase _getAuthorizedSubmissionDetailUseCase;

    public GradingController(
        IGradeSubmissionByTestCaseUseCase gradeSubmissionByTestCaseUseCase,
        IGradeSubmissionByRubricUseCase gradeSubmissionByRubricUseCase,
        IOverrideSubmissionScoreUseCase overrideSubmissionScoreUseCase,
        IReviewAiRubricResultUseCase reviewAiRubricResultUseCase,
        IOverrideRubricCriteriaScoreUseCase overrideRubricCriteriaScoreUseCase,
        IExplainRubricDeductionUseCase explainRubricDeductionUseCase,
        IGetAuthorizedSubmissionDetailUseCase getAuthorizedSubmissionDetailUseCase)
    {
        _gradeSubmissionByTestCaseUseCase = gradeSubmissionByTestCaseUseCase;
        _gradeSubmissionByRubricUseCase = gradeSubmissionByRubricUseCase;
        _overrideSubmissionScoreUseCase = overrideSubmissionScoreUseCase;
        _reviewAiRubricResultUseCase = reviewAiRubricResultUseCase;
        _overrideRubricCriteriaScoreUseCase = overrideRubricCriteriaScoreUseCase;
        _explainRubricDeductionUseCase = explainRubricDeductionUseCase;
        _getAuthorizedSubmissionDetailUseCase = getAuthorizedSubmissionDetailUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid submissionId)
    {
        var (detail, failure) = await EnsureCanAccessSubmissionAsync(submissionId);
        if (failure is not null)
        {
            return failure;
        }

        ViewData["SubmissionDetail"] = detail;

        return View(new GradingPanelViewModel
        {
            SubmissionId = submissionId,
            NewTotalScore = detail.TotalScore
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GradeTestCase(GradingPanelViewModel model)
    {
        var (_, failure) = await EnsureCanAccessSubmissionAsync(model.SubmissionId);
        if (failure is not null)
        {
            return failure;
        }

        try
        {
            await _gradeSubmissionByTestCaseUseCase.HandleAsync(model.SubmissionId);
            SetSuccess("Queued/processed test-case grading.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { submissionId = model.SubmissionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GradeRubric(GradingPanelViewModel model)
    {
        var (_, failure) = await EnsureCanAccessSubmissionAsync(model.SubmissionId);
        if (failure is not null)
        {
            return failure;
        }

        try
        {
            await _gradeSubmissionByRubricUseCase.HandleAsync(model.SubmissionId);
            SetSuccess("Queued/processed rubric grading.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { submissionId = model.SubmissionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OverrideScore(GradingPanelViewModel model)
    {
        var (_, failure) = await EnsureCanAccessSubmissionAsync(model.SubmissionId);
        if (failure is not null)
        {
            return failure;
        }

        if (!ModelState.IsValid)
        {
            SetError("Invalid score override payload.");
            return RedirectToAction(nameof(Index), new { submissionId = model.SubmissionId });
        }

        try
        {
            await _overrideSubmissionScoreUseCase.HandleAsync(model.SubmissionId, model.NewTotalScore, model.OverrideReason);
            SetSuccess("Score overridden successfully.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { submissionId = model.SubmissionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewRubric(GradingPanelViewModel model)
    {
        var (_, failure) = await EnsureCanAccessSubmissionAsync(model.SubmissionId);
        if (failure is not null)
        {
            return failure;
        }

        try
        {
            await _reviewAiRubricResultUseCase.HandleAsync(
                new RubricReviewDecisionDto(
                    model.SubmissionId,
                    model.IsApproved,
                    string.IsNullOrWhiteSpace(model.TeacherComment) ? null : model.TeacherComment));

            SetSuccess(model.IsApproved
                ? "Rubric review approved."
                : "Rubric regrade retry was queued with teacher comment.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { submissionId = model.SubmissionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OverrideRubricCriteria(GradingPanelViewModel model)
    {
        var (_, failure) = await EnsureCanAccessSubmissionAsync(model.SubmissionId);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(model.CriteriaName) || string.IsNullOrWhiteSpace(model.CriteriaOverrideReason))
        {
            SetError("Criteria name and override reason are required.");
            return RedirectToAction(nameof(Index), new { submissionId = model.SubmissionId });
        }

        try
        {
            await _overrideRubricCriteriaScoreUseCase.HandleAsync(
                new OverrideRubricCriteriaScoreRequestDto(
                    model.SubmissionId,
                    model.CriteriaName,
                    model.NewCriteriaScore,
                    model.CriteriaOverrideReason));

            SetSuccess("Rubric criteria score overridden.");
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { submissionId = model.SubmissionId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExplainRubric(GradingPanelViewModel model)
    {
        var (_, failure) = await EnsureCanAccessSubmissionAsync(model.SubmissionId);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(model.CriteriaName) || string.IsNullOrWhiteSpace(model.StudentQuestion))
        {
            SetError("Criteria name and question are required for explanation.");
            return RedirectToAction(nameof(Index), new { submissionId = model.SubmissionId });
        }

        try
        {
            var response = await _explainRubricDeductionUseCase.HandleAsync(
                new ExplainRubricDeductionRequestDto(model.SubmissionId, model.CriteriaName, model.StudentQuestion));

            SetSuccess(response.Explanation);
        }
        catch (DomainException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index), new { submissionId = model.SubmissionId });
    }

    private async Task<(Ports.DTO.Submission.SubmissionDetailDto Detail, IActionResult? Failure)> EnsureCanAccessSubmissionAsync(Guid submissionId)
    {
        if (CurrentUserId is null)
        {
            return (default!, Challenge());
        }

        var authorizedDetail = await _getAuthorizedSubmissionDetailUseCase.HandleAsync(
            submissionId,
            CurrentUserId.Value,
            CurrentUserRoleDto);

        var failure = ToAccessActionResult(authorizedDetail.AccessDecision);
        if (failure is not null)
        {
            return (default!, failure);
        }

        return (authorizedDetail.Submission!, null);
    }
}
