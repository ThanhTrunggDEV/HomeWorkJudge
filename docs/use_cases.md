# Danh sách Use Case

**Ứng dụng hỗ trợ chấm bài tập thực hành lập trình**  
**Tổng cộng:** 24 Use Case | 5 Nhóm

## Nhóm 1 — Quản lý bài tập

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC01 | Tạo bài tập mới | Giáo viên | Nhập tên, đề bài, ngôn ngữ, thời hạn nộp, loại chấm (test case hay rubric) |
| UC02 | Thêm / sửa / xóa test case | Giáo viên | Định nghĩa cặp input/output mẫu cho bài có test case |
| UC03 | Tạo rubric chấm điểm | Giáo viên | Định nghĩa các tiêu chí + trọng số điểm cho bài không có test case |
| UC04 | Publish / ẩn bài tập | Giáo viên | Kiểm soát thời điểm học sinh thấy bài |
| UC05 | Xem danh sách bài tập | Giáo viên, Học sinh | Lọc theo lớp, trạng thái, thời hạn |
| UC05.1 | Chấm lại toàn bộ bài (Re-judge) | Giáo viên | Yêu cầu hệ thống tự động chấm lại tất cả bài nộp của học sinh (thường dùng sau khi cập nhật lại test case bị sai) |

## Nhóm 2 — Nộp bài & Chấm tự động (có test case)

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC06 | Nộp code | Học sinh | Paste code hoặc upload file, chọn ngôn ngữ |
| UC08 | Chấm tự động qua test case | Hệ thống | Chạy từng test case, trả về Passed / Failed / Timeout / Runtime Error |
| UC11 | Giáo viên chấm tay / chỉnh điểm | Giáo viên | Override điểm tự động nếu cần |

## Nhóm 3 — Chấm theo Rubric (không có test case)

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC-R01 | Tạo rubric cho bài tập | Giáo viên | Định nghĩa tiêu chí, mô tả từng mức điểm |
| UC-R02 | AI chấm theo rubric | AI | Đọc đề bài + rubric + code, cho điểm từng tiêu chí kèm lý do |
| UC-R03 | Giáo viên review kết quả AI | Giáo viên | Xem điểm AI đề xuất, đồng ý hoặc chỉnh sửa |
| UC-R04 | Giáo viên override từng tiêu chí | Giáo viên | Chỉnh điểm từng hạng mục trong rubric |
| UC-R05 | Học sinh xem điểm chi tiết theo tiêu chí | Học sinh | Biết rõ bị trừ điểm ở đâu, tại sao |
| UC-R06 | Học sinh hỏi AI lý do trừ điểm | Học sinh | Chat với AI để hiểu sâu hơn về nhận xét |

## Nhóm 4 — Kết quả & Phản hồi

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC12 | Xem kết quả chi tiết từng test case | Học sinh | Thấy input/output kỳ vọng vs thực tế |
| UC13 | Nhận phản hồi & gợi ý từ AI | Học sinh | Gợi ý cách sửa lỗi, tối ưu code |
| UC14 | Xem lịch sử các lần nộp | Học sinh | So sánh điểm qua các lần submit |
| UC15 | Xem bảng điểm cả lớp | Giáo viên | Thống kê điểm, tỷ lệ pass/fail từng bài |
| UC16 | Export báo cáo điểm | Giáo viên | Xuất file CSV/Excel để lưu trữ hoặc nhập điểm hệ thống trường |

## Nhóm 5 — Quản lý người dùng & Lớp học

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC17 | Đăng ký / Đăng nhập | Tất cả | Hỗ trợ email hoặc SSO (Google, GitHub) |
| UC18 | Tạo & quản lý lớp học | Giáo viên | Tạo lớp, gán bài tập, quản lý thành viên |
| UC19 | Tham gia lớp bằng mã | Học sinh | Nhập mã lớp để join |
| UC20 | Phân quyền hệ thống | Admin | Gán vai trò Admin / Giáo viên / Học sinh |

## Tổng hợp

| Nhóm | Tên nhóm | Số UC |
| :--- | :--- | :--- |
| 1 | Quản lý bài tập | 6 |
| 2 | Nộp bài & Chấm tự động | 3 |
| 3 | Chấm theo Rubric | 6 |
| 4 | Kết quả & Phản hồi | 5 |
| 5 | Quản lý người dùng & Lớp học | 4 |

**Tổng cộng:** 24 Use Case
