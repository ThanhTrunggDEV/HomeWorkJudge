# Thiết kế Domain Model theo DDD (Domain-Driven Design)

Mô hình thiết kế Domain Layer cho hệ thống HomeWorkJudge.  
Thư mục Domain sẽ bao gồm các thành phần cốt lõi của **Clean Architecture** & **DDD**: Entities, Value Objects, Domain Events, Policies (Domain Services/Business Rules), và Ports (Interfaces for repositories/services).

---

## 1. Entities
*Các thực thể có định danh (Id) duy nhất và mô phỏng vòng đời của đối tượng.*

**User (Người dùng)**
* Tập trung quản trị thông tin định danh và vai trò.
* **Fields:** Id, Email, FullName, Role (Admin, Teacher, Student).
* **Methods:** UpdateProfile(), ChangeRole().

**Classroom (Lớp học)**
* Nơi giáo viên tổ chức lớp và gom nhóm học viên.
* **Fields:** Id, JoinCode, Name, TeacherId, StudentIds (List).
* **Methods:** GenerateNewJoinCode(), AddStudent(), RemoveStudent().

**Assignment (Bài tập)**
* Là trung tâm (Aggregate Root) của việc ra đề, chứa thông tin chung và cấu hình.
* **Fields:** Id, Title, Description, AllowedLanguages, DueDate, PublishStatus (Draft, Published), ClassroomId.
* **Config Fields:** TimeLimit, MemoryLimit, MaxSubmissions, GradingType (TestCase, Rubric).
* **Methods:** Publish(), UpdateDueDate(), IsOverdue().

**TestCase (Test Case)**
* Thuộc về bài tập, định nghĩa dữ liệu đầu vào.
* **Fields:** Id, AssignmentId, InputData, ExpectedOutput, IsHidden, ScoreWeight.

**Rubric (Tiêu chí chấm điểm)**
* Thuộc về bài tập, sử dụng để AI hoặc Giáo viên đối chiếu lúc chấm.
* **Fields:** Id, AssignmentId, CriteriaList (List of JSON / Object mô tả chi tiết các phân mức từ Tốt đến Yếu cho từng tiêu chí).

**Submission (Bài nộp)**
* Aggregate Root quan trọng nhất, lưu trữ mã nguồn và quy trình chấm.
* **Fields:** Id, AssignmentId, StudentId, SourceCode, Language, SubmitTime, Status (Pending, Executing, Done).
* **Grading Fields:** TotalScore, List<TestCaseResult>, List<RubricResult>.
* **Methods:** ChangeStatusToExecuting(), AttachTestCaseResults(), AttachRubricResults(), CalculateTotalScore().

---

## 2. Value Objects
*Các đối tượng bất biến, không có định danh (Id), được xác định bởi giá trị của chúng, thường dùng làm thuộc tính cho Entity. Trong DDD, định danh của Entity cũng nên được bọc trong Value Object (Strongly-typed ID).*

**Strongly-typed IDs**
* Thay vì dùng `Guid` hay `int` chung chung, mỗi định danh Entity là một kiểu dữ liệu riêng.
* `UserId`, `ClassroomId`, `AssignmentId`, `TestCaseId`, `RubricId`, `SubmissionId`

**TestCaseResult**
* Dữ liệu trả về sau khi chạy thử 1 test case.
* **Fields:** ActualOutput, ExecutionTime, MemoryUsed, Status (Passed, Failed, TimeOut, RuntimeError).

**RubricResult**
* Đánh giá trên 1 tiêu chí cụ thể.
* **Fields:** CriteriaName, GivenScore, CommentReason (Nhận xét của AI / Giáo viên).

---

## 3. Events (Domain Events)
*Các sự kiện phát sinh khi một hành động thay đổi trạng thái quan trọng xảy ra trong Domain, giúp giao tiếp lỏng lẻo (decoupled) sang các Bounded Context/Service khác.*

* **AssignmentPublishedEvent**: Kích hoạt khi Giáo viên Publish bài, có thể bắt sự kiện này để gửi Notification cho học sinh.
* **SubmissionCreatedEvent**: Kích hoạt khi Học sinh nộp bài, Application Layer nhận event này để nhét bài vào Queue (ví dụ RabbitMQ) chờ Sandbox chấm.
* **SubmissionGradingCompletedEvent**: Kích hoạt khi Sandbox hoặc AI chấm xong, chuyển trạng thái Submission thành Done.

---

## 4. Policies (Domain Rules / Domain Services)
*Các nguyên tắc nghiệp vụ phức tạp hoặc liên quan nhiều Entity, không thể nhét riêng vào một Entity nào.*

* **LateSubmissionPolicy**: Tính toán % số điểm bị trừ nếu học sinh nộp bài trễ SubmitTime > Assignment.DueDate.
* **PlagiarismMatchPolicy**: Logic kiểm tra mức độ trùng lặp. Ngưỡng SimilarityScore được cấu hình động qua constructor/DI (không hard-code).

---

## 5. Ports (Interfaces)
*Các cổng giao tiếp (Inversion of Control) để Domain tương tác với thế giới bên ngoài (Database) mà không bị phụ thuộc.*

**Repositories (DB Access):**
* `IUserRepository`
* `IClassroomRepository`
* `IAssignmentRepository`
* `ISubmissionRepository`
* `IUnitOfWork`

