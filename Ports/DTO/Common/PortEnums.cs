namespace Ports.DTO.Common;

public enum AssignmentGradingTypeDto
{
    TestCase = 1,
    Rubric = 2
}

public enum AssignmentPublishStatusDto
{
    Draft = 1,
    Published = 2
}

public enum SubmissionStatusDto
{
    Pending = 1,
    Executing = 2,
    Done = 3
}

public enum TestCaseExecutionStatusDto
{
    Passed = 1,
    Failed = 2,
    TimeOut = 3,
    RuntimeError = 4
}

public enum UserRoleDto
{
    Student = 1,
    Teacher = 2,
    Admin = 3
}
