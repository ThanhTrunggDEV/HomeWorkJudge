# Thiết kế Project Ports (Docs Only)

Tài liệu này mô tả cấu trúc đề xuất cho project Ports theo hướng Clean Architecture.
Phạm vi: **chỉ thiết kế tài liệu**, chưa triển khai code.

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
    Classroom/
    User/

  InBoundPorts/
    Assignment/
    Submission/
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
- `IPublishAssignmentUseCase`
- `IRejudgeAssignmentUseCase`

### 4.2 Submission Inbound
- `ISubmitCodeUseCase`
- `IGetSubmissionDetailUseCase`
- `IGetSubmissionHistoryUseCase`

### 4.3 Grading Inbound
- `IGradeSubmissionByTestCaseUseCase`
- `IGradeSubmissionByRubricUseCase`
- `IOverrideSubmissionScoreUseCase`

### 4.4 Classroom/User Inbound
- `ICreateClassroomUseCase`
- `IJoinClassroomUseCase`
- `IRegisterUserUseCase`
- `ILoginUseCase`

### 4.5 Query/Report Inbound
- `IGetSubmissionResultDetailUseCase`
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

## 6. Quy ước đặt tên

- Inbound: `I<Verb><Noun>UseCase`
- Outbound: `I<Noun><Purpose>Port`
- DTO: `<UseCase><Request|Response>Dto`

## 7. Mapping nhanh theo Use Case hiện tại

- UC01, UC04, UC05.1 -> Assignment Inbound + Queue Outbound (+ Domain Persistence Ports)
- UC06, UC08, UC11 -> Submission/Grading Inbound + Judging Outbound (+ Domain Persistence Ports)
- UC-R01..UC-R06 -> Rubric Inbound + AI Service Outbound + RubricGrading Outbound (+ Domain Persistence Ports)
- UC12..UC15 -> Query Inbound (+ AI Service Outbound cho UC13)
- UC16 -> Report Inbound + ReportExport Outbound
- UC17..UC20 -> User/Classroom Inbound (+ Domain Persistence Ports)

## 8. Kế hoạch triển khai sau tài liệu

1. Tạo folder thật trong project Ports theo cây trên.
2. Khai báo interface trống cho từng Inbound/Outbound.
3. Khai báo DTO theo use case ưu tiên (UC06, UC08, UC11 trước).
4. Kết nối Application layer implement InboundPorts.
5. Kết nối Infrastructure layer implement OutBoundPorts của project Ports + Persistence Ports của Domain.
