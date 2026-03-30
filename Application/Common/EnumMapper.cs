using Domain.ValueObject;
using Ports.DTO.Common;

namespace Application.Common;

internal static class EnumMapper
{
    public static UserRole ToDomain(UserRoleDto role)
        => role switch
        {
            UserRoleDto.Student => UserRole.Student,
            UserRoleDto.Teacher => UserRole.Teacher,
            UserRoleDto.Admin => UserRole.Admin,
            _ => UserRole.Student
        };

    public static UserRoleDto ToDto(UserRole role)
        => role switch
        {
            UserRole.Student => UserRoleDto.Student,
            UserRole.Teacher => UserRoleDto.Teacher,
            UserRole.Admin => UserRoleDto.Admin,
            _ => UserRoleDto.Student
        };

    public static AssignmentGradingTypeDto ToDto(GradingType gradingType)
        => gradingType switch
        {
            GradingType.TestCase => AssignmentGradingTypeDto.TestCase,
            GradingType.Rubric => AssignmentGradingTypeDto.Rubric,
            _ => AssignmentGradingTypeDto.TestCase
        };

    public static GradingType ToDomain(AssignmentGradingTypeDto gradingType)
        => gradingType switch
        {
            AssignmentGradingTypeDto.TestCase => GradingType.TestCase,
            AssignmentGradingTypeDto.Rubric => GradingType.Rubric,
            _ => GradingType.TestCase
        };

    public static AssignmentPublishStatusDto ToDto(PublishStatus status)
        => status switch
        {
            PublishStatus.Draft => AssignmentPublishStatusDto.Draft,
            PublishStatus.Published => AssignmentPublishStatusDto.Published,
            _ => AssignmentPublishStatusDto.Draft
        };

    public static SubmissionStatusDto ToDto(SubmissionStatus status)
        => status switch
        {
            SubmissionStatus.Pending => SubmissionStatusDto.Pending,
            SubmissionStatus.Executing => SubmissionStatusDto.Executing,
            SubmissionStatus.Done => SubmissionStatusDto.Done,
            _ => SubmissionStatusDto.Pending
        };

    public static TestCaseExecutionStatusDto ToDto(TestCaseStatus status)
        => status switch
        {
            TestCaseStatus.Passed => TestCaseExecutionStatusDto.Passed,
            TestCaseStatus.Failed => TestCaseExecutionStatusDto.Failed,
            TestCaseStatus.TimeOut => TestCaseExecutionStatusDto.TimeOut,
            TestCaseStatus.RuntimeError => TestCaseExecutionStatusDto.RuntimeError,
            _ => TestCaseExecutionStatusDto.RuntimeError
        };
}
