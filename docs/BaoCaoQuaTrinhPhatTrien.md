# BÁO CÁO QUÁ TRÌNH PHÁT TRIỂN DỰ ÁN HOMEWORKJUDGE

> **Môn học / Dự án:** Hệ thống chấm bài tập lập trình hỗ trợ AI  
> **Kiến trúc:** Clean Architecture + Domain-Driven Design (DDD)  
> **Nền tảng:** .NET 9 · WPF · SQLite · Google Gemini API  
> **Thời gian:** 2026

---

## MỤC LỤC

1. [Phân tích yêu cầu](#1-phân-tích-yêu-cầu)
2. [Thiết kế mô hình nghiệp vụ](#2-thiết-kế-mô-hình-nghiệp-vụ)
3. [Triển khai tầng Domain](#3-triển-khai-tầng-domain)
4. [Thiết kế và triển khai Ports](#4-thiết-kế-và-triển-khai-ports)
5. [Triển khai Repository và Use Case](#5-triển-khai-repository-và-use-case)
6. [Triển khai Infrastructure](#6-triển-khai-infrastructure)
7. [Triển khai UI](#7-triển-khai-ui)
8. [Kiểm thử](#8-kiểm-thử)
9. [Kết luận](#9-kết-luận)

---

## 1. PHÂN TÍCH YÊU CẦU

### 1.1 Bối cảnh và mục tiêu

HomeWorkJudge là ứng dụng desktop (WPF) chạy offline, hỗ trợ giảng viên chấm bài tập lập trình bằng AI theo thang điểm Rubric. Ứng dụng giải quyết bài toán thực tế: giảng viên phải chấm thủ công hàng chục bài nộp zip từ sinh viên — tốn thời gian, thiếu nhất quán.

**Mục tiêu cốt lõi:**

- Tự động hóa việc đọc source code từ file zip và đánh giá theo Rubric do giảng viên định nghĩa
- Sử dụng AI (Google Gemini) để chấm điểm từng tiêu chí với nhận xét chi tiết
- **Bắt buộc compile thành công** (với C#) trước khi đưa vào AI — đảm bảo không chấm code lỗi cú pháp
- Cho phép giảng viên review, chỉnh điểm và xuất báo cáo

### 1.2 Actors

| Actor | Loại | Vai trò |
|---|---|---|
| **Giảng viên (GV)** | Primary | Người dùng duy nhất: tạo rubric, tạo phiên chấm, review kết quả |
| **AI Service (Gemini)** | External System | Nhận source code + rubric → trả điểm từng tiêu chí + nhận xét |

### 1.3 Yêu cầu chức năng

Hệ thống được thiết kế với **14 Use Case** chia thành 6 nhóm chức năng:

#### Nhóm 1 — Quản lý Rubric
| UC | Tên | Mô tả |
|---|---|---|
| UC-01 | Tạo Rubric thủ công | Nhập tên, thêm tiêu chí (tên, điểm tối đa, mô tả mức điểm) |
| UC-02 | Tạo Rubric bằng AI | GV nhập đề bài → AI tạo bản nháp → GV chỉnh sửa |
| UC-03 | Sửa / Nhân bản Rubric | Cập nhật tiêu chí hoặc clone thành bản mới |
| UC-04 | Xem danh sách Rubric | Tìm kiếm theo tên, xem trước nội dung |

#### Nhóm 2 — Phiên chấm bài
| UC | Tên | Mô tả |
|---|---|---|
| UC-05 | Tạo phiên chấm | Đặt tên, chọn rubric, upload folder chứa file zip của sinh viên |
| UC-06 | Xem danh sách phiên | Tiến độ, số bài đã chấm, số bài chờ review |

#### Nhóm 3 — Chấm bài
| UC | Tên | Mô tả |
|---|---|---|
| UC-07 | Kích hoạt AI chấm | Build C# → AI chấm → lưu kết quả, hiển thị tiến độ realtime |
| UC-08 | Chấm lại (Re-grade) | Chấm lại một bài hoặc toàn bộ phiên |

#### Nhóm 4 — Review kết quả
| UC | Tên | Mô tả |
|---|---|---|
| UC-09 | Xem chi tiết bài nộp | Source code + bảng điểm từng tiêu chí + nhận xét AI |
| UC-10 | Review / Chỉnh điểm | Duyệt điểm AI, sửa từng tiêu chí, hoặc override tổng điểm |

#### Nhóm 5 — Báo cáo
| UC | Tên | Mô tả |
|---|---|---|
| UC-11 | Xem bảng điểm | Bảng tổng hợp: TB, Min, Max, tỷ lệ đạt |
| UC-12 | Export điểm | Xuất CSV/Excel với định danh SV và điểm từng tiêu chí |

#### Nhóm 6 — Cấu hình
| UC | Tên | Mô tả |
|---|---|---|
| UC-13 | Cấu hình AI Provider | Chọn provider, nhập API key, chọn model, test kết nối |

### 1.4 Yêu cầu phi chức năng

| Yêu cầu | Mô tả |
|---|---|
| **Offline-first** | Toàn bộ dữ liệu lưu local (SQLite), không cần server |
| **Build-first gate** | Với dự án C#, bắt buộc build thành công trước khi AI chấm |
| **Timeout & Resilience** | AI call có timeout, retry, không block UI |
| **Bảo mật** | API key lưu trong Windows Credential Store |
| **Hiệu năng** | Chấm song song nhiều bài, UI responsive |

### 1.5 Các khái niệm nghiệp vụ cốt lõi

| Khái niệm | Định nghĩa |
|---|---|
| **Rubric** | Thang điểm gồm nhiều tiêu chí, mỗi tiêu chí có điểm tối đa và mô tả mức đánh giá |
| **RubricCriteria** | Một tiêu chí trong Rubric (VD: "Tính đúng đắn", "Phong cách code") |
| **GradingSession** | Một đợt chấm: tên phiên + Rubric + tập bài nộp |
| **Submission** | Bài nộp của một SV: source code (giải nén từ zip) + kết quả chấm |
| **SourceFile** | Một file source code (tên + nội dung) trong bài nộp |
| **RubricResult** | Kết quả chấm một tiêu chí: điểm được cho + điểm tối đa + nhận xét |
| **BuildLog** | Output của `dotnet build` — dùng chẩn đoán khi bài không compile được |
| **StudentIdentifier** | Tên file zip bỏ phần mở rộng (VD: `2021001.zip` → `2021001`) |

---

## 2. THIẾT KẾ MÔ HÌNH NGHIỆP VỤ

### 2.1 Phương pháp — Domain-Driven Design

Dự án áp dụng **DDD (Domain-Driven Design)** với Clean Architecture 4 tầng:

```
UI (WPF)
    ↓↑  Commands / Queries (DTO)
Application (Use Cases)
    ↓↑  Domain Interfaces (Ports)
Domain (Business Logic)
    ↑
Infrastructure (Adapters: SQLite, Gemini API, dotnet build)
```

**Nguyên tắc thiết kế đã áp dụng:**
- Mỗi Aggregate Root có Repository riêng
- GradingSession **không** chứa `List<Submission>` để tránh aggregate quá lớn
- Submission là AR độc lập, liên kết với Session qua `SessionId`
- Domain Events để thông báo UI theo tiến độ chấm

### 2.2 Sơ đồ Entity-Relationship

```
Rubric ──────(1:N)────── RubricCriteria
   │
   │ (by RubricId)
GradingSession ──────(1:N by SessionId)────── Submission
                                                  │
                                           (1:N) RubricResult
                                           (1:N) SourceFile
```

### 2.3 Aggregate Roots

| Aggregate Root | Chứa | Repository |
|---|---|---|
| **Rubric** | `List<RubricCriteria>` | `IRubricRepository` |
| **GradingSession** | Metadata + ref đến Submission qua SessionId | `IGradingSessionRepository` |
| **Submission** | `List<SourceFile>`, `List<RubricResult>`, `BuildLog` | `ISubmissionRepository` |

### 2.4 Vòng đời Submission (State Machine)

```
                    ┌─────────────────────────────────┐
                    │                                 │
[Import] ──► Pending ──► Grading ──► BuildFailed ──► Pending (Re-grade)
                            │
                    ┌───────┴───────┐
                    ▼               ▼
                 AIGraded         Error
                    │               │
                    └───────┬───────┘
                            ▼
                         Reviewed (GV chốt điểm)
```

**Trạng thái:**
- **Pending**: Đã import, chờ chấm
- **Grading**: Đang trong quá trình xử lý (build + AI)
- **BuildFailed** *(mới)*: Build C# thất bại — điểm = 0, AI không được gọi
- **AIGraded**: AI chấm xong, chờ GV review
- **Error**: AI gặp lỗi (timeout, API error)
- **Reviewed**: GV đã duyệt / chốt điểm

### 2.5 Domain Events

| Event | Raise ở đâu | Mục đích UI |
|---|---|---|
| `SubmissionsImportedEvent` | Constructor `Submission` | Hiển thị danh sách bài mới import |
| `SubmissionGradingStartedEvent` | `StartGrading()` | Hiện progress bar |
| `SubmissionBuildFailedEvent` *(mới)* | `MarkBuildFailed()` | Hiện trạng thái build lỗi |
| `SubmissionAIGradedEvent` | `AttachAIResults()` | Cập nhật tiến độ (x/n bài) |
| `SubmissionAIFailedEvent` | `MarkError()` | Hiện cảnh báo lỗi AI |
| `SubmissionReviewedEvent` | `Approve()` / Override | Cập nhật bảng điểm |

---

## 3. TRIỂN KHAI TẦNG DOMAIN

### 3.1 Cấu trúc project Domain

```
Domain/
├── Entity/
│   ├── EntityBase.cs          # DomainEvents list, Raise(), ClearDomainEvents()
│   ├── Submission.cs          # AR chính
│   ├── Rubric.cs
│   └── GradingSession.cs
├── ValueObject/
│   └── ValueObjects.cs        # StronglyTyped IDs, SourceFile, RubricResult, SubmissionStatus
├── Event/
│   └── Events.cs              # Tất cả Domain Events
├── Exception/
│   └── DomainException.cs
└── Ports/
    └── IRepositories.cs       # IRubricRepository, ISubmissionRepository, IUnitOfWork...
```

### 3.2 Strongly-typed IDs

```csharp
public readonly record struct RubricId(Guid Value);
public readonly record struct RubricCriteriaId(Guid Value);
public readonly record struct GradingSessionId(Guid Value);
public readonly record struct SubmissionId(Guid Value);
```

**Lý do:** Tránh nhầm lẫn khi truyền tham số — compiler báo lỗi nếu pass `RubricId` vào chỗ cần `SubmissionId`.

### 3.3 Submission Aggregate Root — Các method chính

```csharp
public class Submission : EntityBase
{
    public SubmissionId Id { get; }
    public GradingSessionId SessionId { get; }
    public string StudentIdentifier { get; }
    public SubmissionStatus Status { get; private set; }
    public double TotalScore { get; private set; }
    public string? BuildLog { get; private set; }      // Mới: lưu output dotnet build
    public string? ErrorMessage { get; private set; }

    // State transitions
    public void StartGrading()        // Pending/Error/BuildFailed → Grading
    public void MarkBuildFailed(string buildLog)  // Grading → BuildFailed (score=0)
    public void AttachAIResults(...)  // Grading → AIGraded
    public void MarkError(string msg) // Grading → Error
    public void Approve()             // AIGraded → Reviewed (idempotent)
    public void OverrideCriteriaScore(...)  // AIGraded → Reviewed
    public void OverrideTotalScore(...)
    public void ResetForRegrade()     // Any → Pending
    public void AddTeacherNote(...)
    public void FlagAsPlagiarism(...)
    public void ClearPlagiarismFlag()
}
```

### 3.4 Invariants (Bất biến nghiệp vụ)

- `StartGrading()` chỉ được gọi từ `Pending`, `Error`, hoặc `BuildFailed`
- `MarkBuildFailed()` chỉ được gọi khi `Status == Grading`
- `OverrideCriteriaScore()` chỉ được gọi khi `Status == AIGraded`
- Điểm tiêu chí phải trong `[0, MaxScore]`
- `TotalScore >= 0`
- Rubric phải có ít nhất 1 tiêu chí mới dùng được cho phiên chấm

---

## 4. THIẾT KẾ VÀ TRIỂN KHAI PORTS

### 4.1 Nguyên tắc kiến trúc Ports & Adapters

```
Application Layer
    │
    ├── InBound Ports  → Interface mà tầng ngoài (UI) gọi vào
    └── OutBound Ports → Interface mà Application gọi ra Infrastructure
```

**Quy tắc đặt tên:**
- `I<Verb><Noun>UseCase` — InBound (VD: `IGradingUseCaseHandler`)
- `I<Noun><Purpose>Port` — OutBound (VD: `IAiGradingPort`, `ICSharpBuildPort`)

### 4.2 InBound Ports (gọi từ UI vào Application)

| Interface | Chức năng |
|---|---|
| `IGradingUseCaseHandler` | Kích hoạt chấm, chấm lại, review, xuất báo cáo |
| `IRubricUseCaseHandler` | CRUD rubric, clone, tạo bằng AI |
| `ISessionUseCaseHandler` | Tạo/xóa phiên, import bài nộp |
| `IConfigurationUseCaseHandler` | Lưu/đọc cấu hình API key, model |

### 4.3 OutBound Ports (Application gọi ra)

| Interface | Implementation | Ghi chú |
|---|---|---|
| `IAiGradingPort` | `GeminiGradingPort` | Gọi Google Gemini API |
| `IAiRubricGeneratorPort` | `GeminiRubricGeneratorPort` | Tạo rubric bằng AI |
| `ICSharpBuildPort` *(mới)* | `DotnetBuildPort` | Chạy `dotnet build` trên bài nộp C# |
| `IFileExtractorPort` | `ZipFileExtractorPort` | Giải nén zip, lọc file hợp lệ |
| `IPlagiarismDetectionPort` | `LocalPlagiarismDetectionPort` | So sánh similarity nội bộ |
| `IReportExportPort` | `CsvReportExportPort` | Xuất CSV |

### 4.4 Whitelist file khi giải nén (ZipFileExtractorPort)

Để đảm bảo chỉ source code được xử lý (không phải binary, không phải build artifacts):

| Nhóm | Extension |
|---|---|
| C / C++ | `.c`, `.cpp`, `.h`, `.hpp` |
| C# / .NET | `.cs`, `.csproj`, `.sln`, `.slnx` |
| WPF / MAUI | `.xaml`, `.resx` |
| ASP.NET | `.cshtml`, `.razor` |
| Config / Data | `.json`, `.xml`, `.config` |
| Java | `.java` |
| Python | `.py`, `.pyx` |
| Web | `.js`, `.ts`, `.html`, `.css` |
| Docs | `.txt`, `.md` |

**Thư mục bị bỏ qua:** `bin/`, `obj/`, `node_modules/`, `.vs/`, `.idea/`, `__pycache__/`

**Giới hạn kích thước:** Mỗi file không vượt quá 1 MB

### 4.5 DTOs chính

```csharp
// Kết quả chạy build
public record BuildResult(bool Success, string BuildLog);

// Chi tiết một bài nộp trả về UI
public record SubmissionDetailDto(
    Guid Id, string StudentIdentifier, string Status,
    double TotalScore, string? TeacherNote, string? ErrorMessage,
    string? BuildLog,                // ← Mới: output dotnet build
    IReadOnlyList<RubricResultDto> RubricResults,
    IReadOnlyList<SourceFileDto> SourceFiles
);
```

---

## 5. TRIỂN KHAI REPOSITORY VÀ USE CASE

### 5.1 Repository — SQLite via EF Core

**Persistence layer** (`SqliteInfrastructure`):

```
SqliteInfrastructure/
├── AppDbContext.cs               # EF Core DbContext + ApplySchemaMigrationsAsync()
├── PersistenceModel/
│   └── SubmissionRecord.cs       # Flat table record (không dùng EF navigation thẳng)
└── Repository/
    ├── EntityMapper.cs           # Domain ↔ Persistence mapping (có BuildLog)
    ├── SqliteSubmissionRepository.cs
    ├── SqliteRubricRepository.cs
    └── SqliteGradingSessionRepository.cs
```

**Schema migration không dùng EF Migrations** — dùng `ApplySchemaMigrationsAsync()` idempotent:

```csharp
// Thêm cột BuildLog cho DB cũ (an toàn khi chạy nhiều lần)
ALTER TABLE Submissions ADD COLUMN BuildLog TEXT NULL;
-- Nếu cột đã tồn tại → bắt SqliteException "duplicate column" → bỏ qua
```

**Lý do không dùng EF Migrations:** Ứng dụng desktop, không muốn phụ thuộc migration tooling, `EnsureCreated` đủ cho fresh install.

### 5.2 Application Use Cases — GradingUseCaseHandler

**Flow chấm bài (build-first):**

```
StartGradingAsync(sessionId)
    │
    ├─ Load Session, Rubric, Submissions (Status = Pending)
    │
    └─ For each Submission (parallel):
           │
           ├─ submission.StartGrading()
           │
           ├─ ICSharpBuildPort.BuildAsync(sourceFiles)
           │       │
           │  FAIL ─┤─► submission.MarkBuildFailed(buildLog)  // Score = 0, skip AI
           │        │
           │  OK  ──┤─► IAiGradingPort.GradeAsync(sourceFiles, rubricCriteria)
           │               │
           │        OK ────┤──► submission.AttachAIResults(results)
           │               │
           │        FAIL ──┤──► submission.MarkError(errorMessage)
           │
           └─ SaveChangesAsync() + DispatchDomainEvents()
```

**Thiết kế pattern Use Case:**

1. Load aggregate từ repository
2. Gọi domain method (business logic nằm trong Domain)
3. Thu thập Domain Events (snapshot)
4. `SaveChangesAsync()` — persist
5. `DispatchDomainEvents()` — thông báo UI sau khi save thành công

### 5.3 DomainEventDispatcher

```csharp
// Dispatch reflection-based: tìm handler theo type của event
public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct)
{
    foreach (var e in events)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(e.GetType());
        var handlers = serviceProvider.GetServices(handlerType);
        foreach (var handler in handlers)
            await handler.HandleAsync(e, ct);
    }
}
```

---

## 6. TRIỂN KHAI INFRASTRUCTURE

### 6.1 DotnetBuildPort — Build-gate cho C#

**Quy trình `BuildAsync()`:**

```
1. Ghi source files ra thư mục temp  (Path.GetTempPath()/hwjudge/<submissionId>/)
2. Tìm build target ưu tiên: .sln/.slnx → fallback .csproj
3. Nếu không tìm thấy → trả về BuildResult(false, "Không tìm thấy file .sln...")
4. Chạy: dotnet build "<target>" --nologo -v q
   - Timeout: 120 giây
   - Đọc stdout + stderr bằng ReadToEndAsync() (không dùng event-based để tránh race condition)
5. Truncate output xuống 150 dòng cuối (errors luôn ở cuối output)
6. Trả về BuildResult(exitCode == 0, truncatedLog)
7. Dọn dẹp thư mục temp (finally block)
```

**Lý do truncate 150 dòng:** WPF `TextBox` render text synchronously — log quá dài (nghìn dòng) sẽ freeze UI thread khi mở Expander.

### 6.2 GeminiGradingPort — AI Grading

```
Prompt template:
  "Chấm bài tập lập trình sau theo Rubric:
   [Rubric: tiêu chí 1 (max X điểm): mô tả mức điểm]
   [Source code: file1.cs, file2.cs...]
   → Trả về JSON: [{criteriaName, givenScore, comment}]"
```

**Resilience:**
- Timeout: cấu hình qua `InfrastructureOptions`
- Retry: `DefaultOperationExecutor` với exponential backoff
- Parse lỗi JSON → `MarkError` thay vì throw

### 6.3 LocalPlagiarismDetectionPort

Tính similarity bằng **Jaccard similarity** trên token set của source code — so sánh mọi cặp bài trong cùng phiên.

### 6.4 Dependency Injection Registration

```csharp
services.AddScoped<IAiGradingPort, GeminiGradingPort>();
services.AddScoped<ICSharpBuildPort, DotnetBuildPort>();       // ← Mới
services.AddScoped<IFileExtractorPort, ZipFileExtractorPort>();
services.AddScoped<IPlagiarismDetectionPort, LocalPlagiarismDetectionPort>();
services.AddScoped<IReportExportPort, CsvReportExportPort>();
```

---

## 7. TRIỂN KHAI UI

### 7.1 Kiến trúc UI — MVVM

```
HomeWorkJudge.UI/
├── App.xaml.cs                  # Startup: DI, schema migration, error handling
├── Views/
│   ├── MainWindow.xaml          # Navigation shell
│   ├── DashboardView.xaml
│   ├── RubricListView.xaml
│   ├── SessionListView.xaml
│   ├── SessionDetailView.xaml   # Danh sách bài, tiến độ chấm
│   └── SubmissionReviewView.xaml # Review chi tiết bài nộp
└── ViewModels/
    ├── DashboardViewModel.cs
    ├── RubricListViewModel.cs
    ├── SessionDetailViewModel.cs
    └── SubmissionReviewViewModel.cs
```

**Framework UI:** Material Design In XAML (MaterialDesignThemes), AvalonEdit (code viewer), CommunityToolkit.Mvvm.

### 7.2 SubmissionReviewView — Hiển thị Build Output

Khi bài nộp có `Status = BuildFailed`:

```
┌─────────────────────────────────────────────┐
│  🔧  ✕ Build thất bại — 0 điểm             │  ← Banner đỏ
└─────────────────────────────────────────────┘
│  🔧 Xem Build Output                    [∧] │  ← Expander
│  ┌───────────────────────────────────────┐  │
│  │ ❌ Build FAILED (exit 1) | 240 lines  │  │
│  │ ──────────────────────────────────── │  │
│  │ error CS0246: The type or namespace  │  │
│  │ 'Foo' could not be found (are you   │  │
│  │ missing a using directive...)        │  │
│  └───────────────────────────────────────┘  │
```

### 7.3 Schema Migration khi Startup

```csharp
// App.xaml.cs — chạy khi khởi động
await db.Database.EnsureCreatedAsync();     // Fresh DB: tạo đầy đủ schema
await db.ApplySchemaMigrationsAsync();      // Old DB: ALTER TABLE thêm cột BuildLog
```

---

## 8. KIỂM THỬ

### 8.1 Chiến lược kiểm thử

| Tầng | Loại test | Framework |
|---|---|---|
| Domain | Unit test thuần | xUnit |
| Application | Unit test với mock | xUnit + Moq |
| Infrastructure | Integration test (real process/DB) | xUnit |
| UI ViewModels | Unit test với mock | xUnit + Moq |

### 8.2 Test Projects

```
tests/
├── HomeWorkJudge.Domain.Tests/
│   ├── Entities/SubmissionTests.cs       # 27 test cases
│   ├── Entities/RubricTests.cs
│   └── Policies/PoliciesTests.cs
├── HomeWorkJudge.Application.Tests/
│   └── UseCases/GradingUseCaseHandlerTests.cs  # 29 test cases
├── HomeWorkJudge.InfrastructureService.Tests/
│   ├── OutBoundAdapters/Build/DotnetBuildPortTests.cs
│   ├── OutBoundAdapters/Storage/ZipFileExtractorPortTests.cs
│   └── OutBoundAdapters/Plagiarism/LocalPlagiarismDetectionPortTests.cs
├── HomeWorkJudge.SqliteInfrastructure.Tests/
│   └── Repositories/SqliteRepositoryIntegrationTests.cs  # 5 test cases
└── HomeWorkJudge.UI.ViewModels.Tests/
    └── ViewModels/SubmissionReviewViewModelTests.cs  # 18 test cases
```

### 8.3 Điểm đặc biệt trong kiểm thử

**Test build-first flow:**
```csharp
// Xác minh khi build thất bại, AI không được gọi
[Fact]
public async Task StartGradingAsync_WhenBuildFails_ShouldMarkBuildFailedAndNotCallAI()
{
    buildPort.Setup(b => b.BuildAsync(...))
             .ReturnsAsync(new BuildResult(false, "error CS0246"));

    await sut.StartGradingAsync(command);

    // AI KHÔNG được gọi
    aiGrading.Verify(a => a.GradeAsync(...), Times.Never);
    Assert.Equal(SubmissionStatus.BuildFailed, submission.Status);
    Assert.Equal(0, submission.TotalScore);
}
```

**Test schema migration idempotent:**
```csharp
[Fact]
public async Task ApplySchemaMigrationsAsync_ShouldBeIdempotent()
{
    await db.ApplySchemaMigrationsAsync();
    await db.ApplySchemaMigrationsAsync(); // Gọi lần 2 không throw

    // Xác nhận cột BuildLog tồn tại bằng PRAGMA table_info
}
```

**Test whitelist extension mới:**
```csharp
[Fact]
public async Task ExtractAsync_ShouldAllowWpfAndAspNetExtensions()
{
    // Zip chứa App.xaml, Index.cshtml, Counter.razor, appsettings.json, image.png
    var files = await sut.ExtractAsync(zipPath);
    Assert.Contains("App.xaml", names);       // WPF ✓
    Assert.Contains("Index.cshtml", names);    // Razor ✓
    Assert.DoesNotContain("image.png", names); // Binary ✗
}
```

### 8.4 Kết quả coverage

| Test Project | Line Coverage |
|---|---|
| Domain.Tests | **85.7%** |
| Application.Tests | **62.5%** |
| InfrastructureService.Tests | **51.6%** |
| SqliteInfrastructure.Tests | **58.0%** |
| UI.ViewModels.Tests | **13.5%** |

**Tổng số test cases: 118** (tăng từ 66 trước khi thêm build-first feature)

---

## 9. KẾT LUẬN

### 9.1 Tóm tắt những gì đã triển khai

| Giai đoạn | Nội dung | Trạng thái |
|---|---|---|
| Phân tích | 14 Use Case, 6 nhóm chức năng, khái niệm nghiệp vụ | ✅ Hoàn thành |
| Domain Model | 3 Aggregate Root, State Machine, Domain Events | ✅ Hoàn thành |
| Ports | InBound + OutBound interfaces, DTO | ✅ Hoàn thành |
| Repository & Use Case | SQLite EF Core, GradingUseCaseHandler | ✅ Hoàn thành |
| Infrastructure | Gemini API, dotnet build, zip extractor, plagiarism, CSV export | ✅ Hoàn thành |
| Build-First Gate | `ICSharpBuildPort` → `DotnetBuildPort` | ✅ Hoàn thành |
| UI | WPF MVVM, Material Design, build output viewer | ✅ Hoàn thành |
| Test | 118 test cases, Domain 85.7% coverage | ✅ Hoàn thành |

### 9.2 Điểm nổi bật về kỹ thuật

1. **Build-First Gate** — Tính năng đặc thù: bắt buộc compile trước AI, tránh chấm code lỗi cú pháp
2. **Idempotent Schema Migration** — Không dùng EF Migrations, thay bằng raw SQL `ALTER TABLE IF NOT EXIST` pattern
3. **Race-condition-free output capture** — `ReadToEndAsync()` thay vì event-based `BeginOutputReadLine()` để tránh lose output
4. **Log truncation** — Giữ 150 dòng cuối build log để tránh WPF TextBox freeze UI
5. **Strongly-typed IDs** — `record struct RubricId(Guid)` thay `Guid` thuần để tránh nhầm tham số
6. **Domain Events** — Thông báo UI tiến độ chấm real-time mà không phụ thuộc framework ngoài

### 9.3 Hướng phát triển tiếp theo

- Tăng coverage Application.Tests và UI.ViewModels.Tests lên > 80%
- Hỗ trợ thêm ngôn ngữ build gate (Java với `javac`, Python với `py_compile`)
- Thêm chức năng comment inline trên source code
- Tích hợp thêm AI provider (OpenAI GPT-4o)
- Export báo cáo định dạng Excel (hiện chỉ có CSV)

---

*Báo cáo được tạo tự động từ tài liệu thiết kế và source code của dự án HomeWorkJudge.*  
*Ngày tạo: 01/04/2026*
