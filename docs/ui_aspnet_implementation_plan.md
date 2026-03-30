# Ke hoach trien khai tang UI ASP.NET MVC

Tai lieu nay mo ta lo trinh trien khai giao dien ASP.NET MVC cho HomeWorkJudge, theo huong di tu MVP den san sang van hanh.

## 1. Muc tieu

- Co luong UI end-to-end cho cac use case chinh: Auth -> Classroom -> Assignment -> Submission -> Grading -> Report.
- Tach ro ViewModel/UI logic voi Application use case.
- Giu duoc kha nang mo rong sang API hoac SPA sau nay.

## 2. Nguyen tac thuc hien

- UI chi goi InBound use cases, khong truy cap truc tiep Domain repository.
- Moi man hinh co ViewModel rieng, validate bang DataAnnotations.
- Mapping loi nghiep vu ve thong bao than thien cho nguoi dung.
- Uu tien tinh on dinh va ro luong nghiep vu truoc khi toi uu giao dien.

## 3. Lo trinh trien khai

### Phase 1 - Foundation UI

Muc tieu:
- Dung bo khung web host va DI de chay duoc luong MVC + Application + Infrastructure.

Cong viec:
- Wire DI trong Program: AddApplicationUseCases, AddInfrastructureServiceFoundation, DbContext va repositories.
- Cap nhat HomeWorkJudge.csproj voi cac ProjectReference can thiet.
- Chot migration/bootstrap database:
  - Development: auto apply migration khi startup.
  - Production: script migration duoc version hoa va run theo pipeline.
- Seed du lieu toi thieu cho smoke test (teacher/student/classroom mau).
- Them health check co ban cho DB, Queue, AI provider.
- Tao base layout, partial alert, error page thong nhat.
- Thiet lap ViewModel base + helper map DomainException.

Deliverable:
- App chay duoc voi dependency day du.
- Migration + seed toi thieu chay duoc tren moi truong dev.
- Co khung UI thong nhat cho cac trang tiep theo.

### Phase 2 - Auth va User

Muc tieu:
- Hoan thanh dang ky/dang nhap va trang thong tin nguoi dung cho MVP.

Cong viec:
- Trang Register, Login, Logout.
- Session/Cookie auth cho MVP voi claim identity ro rang.
- Bat anti-forgery cho tat ca form POST.
- Chuan hoa cookie security flags: HttpOnly, Secure, SameSite.
- Bat buoc validation server-side cho tat ca input auth.
- Trang Profile (xem thong tin co ban).
- Guard role o controller + use case; UI chi dung de an/hien action.

Deliverable:
- User co the tao tai khoan, dang nhap, dang xuat.

### Phase 3 - Classroom

Muc tieu:
- Hoan thanh quan ly lop hoc va tham gia lop.

Cong viec:
- Trang tao lop, danh sach lop cua user.
- Join classroom bang join code.
- Trang chi tiet lop (thanh vien, assignment tong quan).

Deliverable:
- Teacher tao lop, Student tham gia lop, xem duoc danh sach lien quan.

### Phase 4 - Assignment

Muc tieu:
- Hoan thanh thao tac assignment cho teacher va hien thi cho student.

Cong viec:
- Teacher: tao/sua assignment.
- Quan ly test case hoac rubric.
- Publish assignment.
- Student: xem chi tiet assignment va deadline.

Deliverable:
- Assignment day du tu draft den published.

### Phase 5 - Submission va Grading

Muc tieu:
- Hoan thanh luong nop bai va xem ket qua cham.

Cong viec:
- Student nop source code theo assignment.
- Trang theo doi trang thai grading.
- Teacher xem chi tiet submission, override score, review AI rubric.
- Refresh/polling nhe de cap nhat ket qua.

Deliverable:
- Luong nop bai va cham bai hoat dong end-to-end.

### Phase 6 - Query va Report

Muc tieu:
- Hoan thanh cac trang tra cuu va xuat bao cao.

Cong viec:
- Scoreboard theo classroom/assignment.
- Submission history theo user.
- Export CSV report tu UI.

Deliverable:
- Co dashboard truy van ket qua va xuat bao cao.

### Phase 7 - Hardening UI

Muc tieu:
- Nang cao UX, bao mat, va do ben.

Cong viec:
- Empty/loading/error states day du.
- Responsive mobile/tablet.
- Rasoat bao mat nang cao: CSP, rate-limit endpoint nhay cam, log hygiene.
- Chuan hoa validation message, input sanitation va error boundary.
- Smoke test luong chinh va test regression co ban.

Deliverable:
- UI on dinh de demo va tiep tuc phat trien.

## 4. Thu tu uu tien backlog

1. Wire host + DI (Phase 1).
2. Auth pages (Phase 2).
3. Classroom list/detail (Phase 3).
4. Assignment create/publish (Phase 4).
5. Submission + grading detail (Phase 5).
6. Scoreboard + report export (Phase 6).

## 5. Definition of Done cho tang UI

- Moi use case quan trong deu co man hinh va luong thao tac tuong ung.
- Form validation hoat dong client/server.
- Luu thong bao loi ro rang, khong lo thong tin nhay cam.
- Build pass va smoke test dat.

## 5.1 Acceptance criteria theo phase

Phase 1:
- App startup pass voi DI day du (Application + Infrastructure + SqliteInfrastructure).
- Migration/seed dev pass tren may moi.
- Health checks tra ve trang thai dung (db/queue/ai).

Phase 2:
- Dang ky/dang nhap/dang xuat pass.
- Tat ca form auth POST co anti-forgery.
- Request trai role bi chan tai controller/use case.

Phase 3:
- Teacher tao classroom duoc.
- Student join classroom bang join code hop le.
- Classroom detail hien dung member/assignment tong quan.

Phase 4:
- Teacher tao/sua/publish assignment pass.
- TestCase hoac Rubric duoc luu va hien thi dung.
- Student xem assignment detail duoc theo role.

Phase 5:
- Student submit code duoc va thay trang thai grading.
- Teacher xem submission detail va override score duoc.
- Luong review AI rubric va retry co thong bao trang thai ro rang.

Phase 6:
- Scoreboard hien dung theo classroom/assignment.
- Submission history phan trang pass.
- Export CSV tu UI tai duoc va dung schema.

## 6. Rui ro va giam thieu

- Rui ro: DI host chua wire dung lam UI goi use case that bai.
  - Giam thieu: Hoan thanh va test Phase 1 truoc khi code man hinh nghiep vu.

- Rui ro: UI guard bi bypass bang request truc tiep.
  - Giam thieu: Bat buoc authorization o controller va use case, UI guard chi mang tinh UX.

- Rui ro: Luong grading phu thuoc queue/AI provider ngoai.
  - Giam thieu: Hien thi trang thai bat dong bo, cho phep retry tu UI voi role teacher.

- Rui ro: Trang thai nghiep vu khong dong bo sau cac thay doi nhanh.
  - Giam thieu: Dung optimistic refresh va thong bao trang thai ro rang.

## 7. De xuat commit groups

- feat(web): wire host with application and infrastructure services
- feat(web): add auth pages and session flow
- feat(web): add classroom pages and join flow
- feat(web): add assignment management UI
- feat(web): add submission and grading UI
- feat(web): add scoreboard and report pages
- chore(web): polish layout, validation, and error handling
