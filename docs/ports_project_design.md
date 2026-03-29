# Thiết kế Project Ports

Tài liệu này mô tả cấu trúc project Ports theo hướng Clean Architecture.
Phạm vi: thiết kế contract và phản ánh hiện trạng đã triển khai trong code.

## 1. Mục tiêu

Project Ports là nơi đặt các contract giao tiếp giữa các tầng:
- **InBoundPorts**: các use case mà tầng ngoài (Web/API/Worker) gọi vào Application.
- **OutBoundPorts**: các cổng Application gọi ra ngoài (queue, sandbox, AI service, file export...).
- **DTO**: dữ liệu vào/ra giữa adapters và use case, tránh lộ Entity trực tiếp ra ngoài.

Lưu ý kiến trúc:
- **Persistence ports (Repository + UnitOfWork) thuộc Domain**, không đặt trong project Ports này.

## 2. Cấu trúc thư mục đề xuất

```text
Ports/
  DTO/
    Common/
    Assignment/
    Submission/
    Rubric/
    Report/
    AI/
    Classroom/
    User/

  InBoundPorts/
    Assignment/
    Submission/
    Grading/
    Query/
    Classroom/
    User/
    Report/

  OutBoundPorts/
    Judging/
    AI/
    RubricGrading/
    Plagiarism/
    Queue/
    Storage/
    Report/
```

## 3. Thiết kế DTO

Nguyên tắc:
- DTO chỉ chứa dữ liệu trao đổi, không chứa business rule.
- Request/Response tách riêng.
- Dùng tên rõ ngữ cảnh theo use case.

Ví dụ DTO theo ngữ cảnh:

### 3.1 Assignment DTO
- `CreateAssignmentRequestDto`
- `CreateAssignmentResponseDto`
- `PublishAssignmentRequestDto`
- `AssignmentDetailDto`

### 3.2 Submission DTO
- `SubmitCodeRequestDto`
- `SubmitCodeResponseDto`
- `SubmissionDetailDto`
- `TestCaseExecutionResultDto`

Ghi chú:
- `SubmissionDetailDto` có thể chứa thêm `AiFeedbackDto` (nullable) để phục vụ UC13.

### 3.3 Rubric DTO
- `RubricCriteriaDto`
- `RubricScoreDto`

### 3.4 Common DTO
- `PagedRequestDto`
- `PagedResponseDto<T>`
- `ErrorDto`

### 3.5 AI DTO
- `AiGradeSubmissionRequestDto`
- `AiGradeSubmissionResponseDto`
- `AiFeedbackDto`
- `AiRubricScoreDto`

## 4. Thiết kế InBoundPorts

InBoundPorts là các contract use case của hệ thống.

Đề xuất nhóm interface:

### 4.1 Assignment Inbound
- `ICreateAssignmentUseCase`
- `IUpdateAssignmentUseCase`
- `IListAssignmentsUseCase`
- `IPublishAssignmentUseCase`
- `IAddAssignmentTestCaseUseCase`
- `IUpdateAssignmentTestCaseUseCase`
- `IDeleteAssignmentTestCaseUseCase`
- `ICreateAssignmentRubricUseCase`
- `IUpdateAssignmentRubricUseCase`
- `IRejudgeAssignmentUseCase`

### 4.2 Submission Inbound
- `ISubmitCodeUseCase`

### 4.3 Grading Inbound
- `IGradeSubmissionByTestCaseUseCase`
- `IGradeSubmissionByRubricUseCase`
- `IOverrideSubmissionScoreUseCase`
- `IReviewAiRubricResultUseCase`
- `IOverrideRubricCriteriaScoreUseCase`
- `IExplainRubricDeductionUseCase`

### 4.4 Classroom/User Inbound
- `ICreateClassroomUseCase`
- `IJoinClassroomUseCase`
- `IRegisterUserUseCase`
- `ILoginUseCase`
- `IAssignUserRoleUseCase`

### 4.5 Query/Report Inbound
- `IGetSubmissionDetailUseCase`
- `IGetScoreboardUseCase`
- `IGetSubmissionHistoryUseCase`
- `IExportScoreReportUseCase`

## 5. Thiết kế OutBoundPorts

OutBoundPorts là contract đi ra hệ thống ngoài.

### 5.1 Persistence (thuộc Domain)
- Sử dụng các interface đã nằm ở project Domain, file [Domain/Ports/IRepositories.cs](Domain/Ports/IRepositories.cs).
- Project Ports này không khai báo lại repository port để tránh trùng ownership.

### 5.2 Judging (Test case)
- `ICodeExecutionPort`
- `ICodeCompilationPort`
- `ITestCaseJudgePort`

Đầu ra tối thiểu cần có:
- status (Passed/Failed/Timeout/RuntimeError)
- actual output
- execution time
- memory usage

### 5.3 Rubric Grading
- `IRubricGradingPort`

### 5.4 AI Service
- `IAiGradingPort`

Vai trò:
- Nhận source code + đề bài + rubric (nếu có)
- Trả về điểm theo tiêu chí + nhận xét
- Cho phép dùng cho UC-R02 và UC13

Contract đề xuất:

```csharp
public interface IAiGradingPort
{
  Task<AiGradeSubmissionResponseDto> GradeSubmissionAsync(
    AiGradeSubmissionRequestDto request,
    CancellationToken cancellationToken = default);
}
```

Nguyên tắc kỹ thuật bắt buộc:
- Có timeout riêng cho mỗi request AI.
- Có retry policy (giới hạn số lần thử lại).
- Có correlation id để trace.
- Không ném raw exception từ SDK ra Application, chuẩn hóa về lỗi nghiệp vụ.

### 5.5 Plagiarism
- `IPlagiarismDetectionPort`

### 5.6 Queue/Storage/Report
- `IBackgroundJobQueuePort`
- `IFileStoragePort`
- `IReportExportPort`

Ghi chú kỹ thuật:
- Queue dùng `JobEnvelopeDto` thay vì payload string rời để chuẩn hóa metadata (correlation id, retry count, created time).
- Storage dùng `Stream` cho upload/download để tránh nạp toàn bộ file vào RAM.

## 6. Quy ước đặt tên

- Inbound: `I<Verb><Noun>UseCase`
- Outbound: `I<Noun><Purpose>Port`
- DTO: `<UseCase><Request|Response>Dto`

## 7. Mapping nhanh theo Use Case hiện tại

- UC01, UC04, UC05, UC05.1 -> Assignment Inbound + Queue Outbound (+ Domain Persistence Ports)
- UC02, UC03 -> Assignment Inbound (test case/rubric management) (+ Domain Persistence Ports)
- UC06, UC08, UC11 -> Submission/Grading Inbound + Judging Outbound (+ Domain Persistence Ports)
- UC-R01 -> Assignment Inbound (rubric setup) (+ Domain Persistence Ports)
- UC-R02 -> Grading Inbound + AI Service Outbound + RubricGrading Outbound (+ Domain Persistence Ports)
- UC-R03, UC-R04, UC-R06 -> Grading Inbound + AI Service Outbound (+ Domain Persistence Ports)
- UC12..UC15 -> Query Inbound (+ AI Service Outbound cho UC13)
- UC16 -> Report Inbound + ReportExport Outbound
- UC17..UC20 -> User/Classroom Inbound (+ Domain Persistence Ports)

## 8. Trạng thái và bước tiếp theo

1. Cấu trúc folder Ports và các contract DTO/InBound/OutBound đã được khai báo.
2. Bước tiếp theo: implement Application layer cho InBoundPorts theo từng nhóm use case ưu tiên.
3. Bước tiếp theo: implement Infrastructure adapters cho OutBoundPorts và wiring DI.
4. Bước tiếp theo: bổ sung test contract-level cho serializer, mapping và boundary validation.
