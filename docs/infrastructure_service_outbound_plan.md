# Ke hoach trien khai InfrastructureService (OutboundPorts)

Tai lieu nay mo ta ke hoach trien khai InfrastructureService theo huong OutboundPorts, uu tien unblock luong cham bai truoc.

## 1. Muc tieu

- Hoan thanh implementation cho toan bo OutboundPorts trong project InfrastructureService.
- Chuan hoa timeout, retry, logging, error mapping cho tat ca adapter.
- Chay duoc luong MVP: submit code -> judge -> luu ket qua -> tra cuu/export co ban.

## 2. Pham vi ports can implement

- Judging
  - ICodeCompilationPort
  - ICodeExecutionPort
  - ITestCaseJudgePort
- AI
  - IAiGradingPort
- Rubric
  - IRubricGradingPort
- Plagiarism
  - IPlagiarismDetectionPort
- Queue
  - IBackgroundJobQueuePort
- Storage
  - IFileStoragePort
- Report
  - IReportExportPort

## 3. Nguyen tac ky thuat

- Moi adapter phai nhan CancellationToken va co timeout rieng.
- Co retry policy co gioi han, khong retry vo han.
- Co correlation id trong log va tracing.
- Khong nem raw exception tu SDK ra Application; map ve loi nghiep vu/ha tang ro rang.
- DTO vao/ra phai khop contract trong project Ports.

## 3.1 Quyet dinh kien truc da chot

- Scope test:
  - Tat ca adapter OutboundPorts deu phai co test truoc khi dong phase (khong chi adapter uu tien).
- Rubric adapter:
  - Chon huong Hybrid Orchestration cho IRubricGradingPort.
  - Rule-based la baseline bat buoc de dam bao tinh on dinh/khong ngau nhien.
  - AI duoc dung de bo sung nhan xet, goi y va tinh diem nang cao.
  - Khi AI fail/timeout: fallback ve Rule-based de khong block luong cham.
- Queue consumer:
  - Production: tach worker rieng de consume queue (de scale doc lap voi web).
  - Development: cho phep in-process BackgroundService de giam do phuc tap local.

## 3.2 Cong nghe de xuat (chot)

- Message broker: RabbitMQ.
- Message bus library: MassTransit.
- Worker process: .NET Worker Service.
- Development mode:
  - Mac dinh van dung RabbitMQ de giam do lech hanh vi giua dev/prod.
  - Cho phep bat InProcess BackgroundService bang config khi can debug nhanh local.

Ly do chon:
- RabbitMQ + MassTransit ho tro retry, dead-letter, outbox pattern, observability tot.
- Worker Service tach doc lap voi web de scale theo queue depth.
- Van giu duoc che do in-process cho local velocity.

## 4. Kien truc de xuat cho InfrastructureService

```text
InfrastructureService/
  OutBoundAdapters/
    Judging/
    AI/
    RubricGrading/
    Plagiarism/
    Queue/
    Storage/
    Report/
  Configuration/
    Options/
    ServiceCollectionExtensions.cs
  Common/
    Errors/
    Resilience/
    Observability/
```

## 5. Lo trinh trien khai theo phase

### Phase 1 - Foundation

Muc tieu:
- Chuan bi nen tang cau hinh va DI.

Cong viec:
- Them project reference toi Ports.
- Tao Options cho tung adapter trong appsettings.
- Tao abstraction chung cho timeout/retry/logging.
- Tao ServiceCollection extension de dang ky adapters.

Deliverable:
- Build pass.
- Co bo khung dang ky DI cho tat ca OutboundPorts.

### Phase 2 - Adapter uu tien cao (de unblock MVP)

Muc tieu:
- Co ngay cac adapter de chay duoc luong co ban.

Cong viec:
- Queue adapter (RabbitMQ publisher mac dinh; cho phep in-memory fallback khi can test nhanh local).
- Queue consumer worker (in-process cho dev, worker rieng cho production).
- Storage adapter (local file system, stream upload/download).
- Report adapter (xuat CSV truoc).

Task ky thuat cu the:
- Tao publisher adapter `RabbitMqBackgroundJobQueueAdapter` implement `IBackgroundJobQueuePort`.
- Tao contract message dung `JobEnvelopeDto` (giu schema nhat quan voi Ports.DTO.Common).
- Tao worker consumer `BackgroundJobConsumer` route theo `JobName` -> handler.
- Tao in-process consumer `InProcessJobBackgroundService` (chi bat trong Development neu config cho phep).
- Them retry policy + dead-letter queue:
  - Retry: exponential backoff, toi da 3 lan.
  - Dead-letter: queue rieng theo namespace `homeworkjudge.dlq`.

Deliverable:
- Co the enqueue job.
- Co consumer xu ly job va retry co gioi han.
- Co the luu/tai file.
- Co the export bang diem CSV.
- Co dashboard co ban de theo doi queue depth va failed messages.

### Phase 3 - Judging adapter

Muc tieu:
- Chay duoc compile/execute/judge test case.

Cong viec:
- Implement compile service.
- Implement execute service co timeout.
- Implement test case judge gom ket qua cho tung test case.
- Chuan hoa mapping trang thai Passed/Failed/TimeOut/RuntimeError.

Deliverable:
- Luong cham test case hoat dong end-to-end.

### Phase 4 - AI, Rubric, Plagiarism

Muc tieu:
- Ho tro grading theo rubric va kiem tra dao van.

Cong viec:
- Implement IAiGradingPort (co fallback/mocking theo moi truong).
- Implement IRubricGradingPort theo huong Hybrid Orchestration:
  - Rule-based engine tinh diem baseline.
  - AI evaluator bo sung nhan xet va goi y cai thien.
  - Fallback ve Rule-based neu AI timeout/fail.
- Implement IPlagiarismDetectionPort (ban dau co the dung similarity noi bo).

Task ky thuat cu the cho Rubric Hybrid:
- Tach nho thanh 3 thanh phan:
  - `RuleBasedRubricScorer`: tinh diem xac dinh theo rubric criteria.
  - `AiRubricAugmentor`: goi IAiGradingPort de bo sung feedback/normalization.
  - `HybridRubricGradingAdapter`: orchestration + merge ket qua + fallback.
- Chinh sach merge ket qua rubric:
  - Tong diem baseline tu Rule-based la nguon su that toi thieu.
  - AI chi duoc dieu chinh trong bien do cau hinh (vi du +/- 10%).
  - Neu AI loi: giu nguyen baseline va gan co `AiUnavailable` trong metadata log.
- Time budget:
  - Rule-based chay sync nhanh.
  - AI bi gioi han timeout rieng (vi du 20s).
  - Qua timeout thi bo qua AI, khong fail toan bo grading.

Deliverable:
- Co ket qua rubric score va plagiarism score o muc MVP.

### Phase 5 - Hardening va test

Muc tieu:
- Tang do tin cay va kha nang van hanh.

Cong viec:
- Contract tests cho tung outbound adapter.
- Integration tests cho tat ca nhom adapter: queue/storage/report/judging/ai/rubric/plagiarism.
- Failure-path tests: timeout, retry exhausted, provider unavailable.
- Hoan thien logging va metrics.

Deliverable:
- Test pass cho cac path quan trong.
- Co playbook xu ly loi adapter.

## 6. Cau hinh de xuat trong appsettings

```json
{
  "Infrastructure": {
    "Queue": {
      "Provider": "RabbitMq",
      "ConsumerMode": "Worker",
      "AllowInProcessInDevelopment": true,
      "RabbitMq": {
        "Host": "localhost",
        "Port": 5672,
        "Username": "guest",
        "Password": "guest",
        "QueueName": "homeworkjudge.jobs",
        "DeadLetterQueueName": "homeworkjudge.dlq"
      },
      "MaxRetryCount": 3
    },
    "Storage": {
      "Provider": "Local",
      "RootPath": "storage"
    },
    "Report": {
      "DefaultFormat": "csv"
    },
    "Judging": {
      "CompileTimeoutSeconds": 10,
      "ExecuteTimeoutSeconds": 5,
      "RetryCount": 1
    },
    "AI": {
      "Provider": "Mock",
      "TimeoutSeconds": 20,
      "RetryCount": 1
    },
    "Rubric": {
      "Mode": "Hybrid",
      "RuleEngine": {
        "Enabled": true,
        "MinDeterministicWeight": 0.6
      },
      "AI": {
        "Enabled": true,
        "MaxAdjustmentPercent": 0.1,
        "TimeoutSeconds": 20,
        "RetryCount": 1
      }
    },
    "Plagiarism": {
      "Provider": "Local",
      "Threshold": 0.8
    }
  }
}
```

## 7. Definition of Done cho giai doan InfrastructureService

- Tat ca OutboundPorts deu co implementation cu the va duoc dang ky DI.
- Co timeout + retry + logging cho moi adapter.
- Luong MVP submit -> judge -> query result chay duoc.
- Tat ca adapter deu co contract tests + integration tests + failure-path tests toi thieu.
- Co hai mode queue da verify:
  - Development: in-process consumer hoat dong khi bat config.
  - Production: worker process rieng consume qua RabbitMQ.
- Rubric Hybrid da verify 3 kich ban:
  - Rule-based only.
  - Rule-based + AI success.
  - Rule-based + AI timeout/fail (fallback khong fail request).

## 7.1 Test matrix toi thieu

- Queue adapter:
  - Publish thanh cong.
  - Consumer xu ly job thanh cong.
  - Retry + dead-letter khi consumer throw lien tiep.
- Storage adapter:
  - Upload stream + download stream toan ven noi dung.
  - Xu ly file key khong ton tai.
- Report adapter:
  - Export CSV dung schema.
  - Xu ly data rong.
- Judging adapters:
  - Compile success/fail.
  - Execute timeout/runtime error.
  - JudgeAsync tra du danh sach ket qua test case.
- AI adapter:
  - Success response map dung DTO.
  - Timeout/retry exhausted map dung loi.
- Rubric Hybrid adapter:
  - Baseline score on dinh voi cung input.
  - AI augmentation trong bien do cho phep.
  - Fallback khi AI fail.
- Plagiarism adapter:
  - Similarity score trong [0, 1].
  - Nguong canh bao hoat dong dung config.

## 8. Thu tu commit de xuat

- feat(infra): scaffold outbound adapters and options
- feat(infra): implement queue storage report adapters
- feat(infra): implement judging adapter with timeout and retries
- feat(infra): implement ai rubric plagiarism adapters
- test(infra): add contract and integration tests
- chore(web): wire outbound adapters in Program

## 9. Rui ro va giam thieu

- Rui ro phu thuoc provider ngoai (AI/Judging):
  - Giam thieu bang timeout, retry gioi han, fallback.
- Rui ro race condition queue va file I/O:
  - Giam thieu bang lock strategy va idempotency key.
- Rui ro leak chi tiet ha tang sang Application:
  - Giam thieu bang error mapping va adapter boundary ro rang.
