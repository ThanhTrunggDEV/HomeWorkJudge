using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ports.DTO.Grading;
using Ports.DTO.Submission;

namespace Ports.InBoundPorts.Grading;

public interface IGradingUseCase
{
    // UC-07
    Task<StartGradingResult> StartGradingAsync(StartGradingCommand command, CancellationToken ct = default);

    // UC-08
    Task RegradeSubmissionAsync(RegradeSubmissionCommand command, CancellationToken ct = default);
    Task RegradeSessionAsync(RegradeSessionCommand command, CancellationToken ct = default);

    // UC-09
    Task<SubmissionDetailDto> GetSubmissionDetailAsync(Guid submissionId, CancellationToken ct = default);
    Task<IReadOnlyList<SubmissionSummaryDto>> GetSubmissionsBySessionAsync(Guid sessionId, CancellationToken ct = default);

    // UC-10
    Task ApproveAsync(ApproveSubmissionCommand command, CancellationToken ct = default);
    Task OverrideCriteriaScoreAsync(OverrideCriteriaScoreCommand command, CancellationToken ct = default);
    Task OverrideTotalScoreAsync(OverrideTotalScoreCommand command, CancellationToken ct = default);
    Task AddTeacherNoteAsync(AddTeacherNoteCommand command, CancellationToken ct = default);

    // Plagiarism
    Task<CheckPlagiarismResult> CheckPlagiarismAsync(CheckPlagiarismCommand command, CancellationToken ct = default);
}
