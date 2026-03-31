# Danh sách Use Case

**Ứng dụng local hỗ trợ giảng viên chấm bài tập lập trình bằng AI theo Rubric**
**Tổng cộng:** 14 Use Case | 6 Nhóm

---

## Actors

| Actor | Loại | Mô tả |
| :--- | :--- | :--- |
| Giảng viên (GV) | Primary | Người dùng duy nhất thao tác trên hệ thống |
| AI Service | External | API bên ngoài (OpenAI / Gemini) — tạo rubric gợi ý và chấm bài |

---

## Luồng nghiệp vụ chính

```
1. GV tạo / chọn Rubric
        ↓
2. GV tạo phiên chấm (đặt tên session + chọn rubric + upload folder)
        ↓
3. GV kích hoạt AI chấm bài
        ↓
4. GV review kết quả, chỉnh điểm nếu cần
        ↓
5. GV xem bảng điểm / export
```

---

## Nhóm 1 — Quản lý Rubric

> Rubric là độc lập, có thể tái sử dụng cho nhiều phiên chấm.
> GV có thể tạo tay hoặc nhờ AI tạo bản nháp rồi chỉnh lại.

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC-01 | Tạo Rubric thủ công | GV | Nhập tên rubric, thêm từng tiêu chí (tên, điểm tối đa, mô tả mức điểm), lưu lại để tái sử dụng |
| UC-02 | Tạo Rubric bằng AI gợi ý | GV, AI | GV nhập đề bài / mô tả yêu cầu → AI tạo bản nháp rubric → GV chỉnh sửa → lưu lại |
| UC-03 | Sửa / Nhân bản Rubric | GV | Sửa tiêu chí trong rubric đã lưu, hoặc nhân bản (clone) thành bản sao mới để chỉnh mà không ảnh hưởng bản gốc |
| UC-04 | Xem danh sách Rubric | GV | Xem tất cả rubric đã lưu, tìm kiếm theo tên, xem trước nội dung |

---

## Nhóm 2 — Phiên chấm bài

> Một phiên chấm = 1 đợt bài nộp + 1 rubric.

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC-05 | Tạo phiên chấm mới | GV | Đặt tên phiên (VD: "Bài 1 - Lớp CS101"), chọn rubric, upload folder bài nộp (mỗi file zip/rar = 1 SV, tên file = định danh SV) |
| UC-06 | Xem danh sách phiên chấm | GV | Xem các phiên đã tạo: tên, rubric, số bài nộp, số bài đã chấm, số bài chờ review |

---

## Nhóm 3 — Chấm bài

> GV kích hoạt chấm, AI xử lý từng bài theo rubric.
> Hệ thống lưu kết quả và chờ GV review.

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC-07 | Kích hoạt AI chấm bài | GV, AI | GV bấm "Chấm bài" → AI lần lượt đọc code + rubric → cho điểm + nhận xét từng tiêu chí → lưu kết quả, hiển thị tiến độ realtime |
| UC-08 | Chấm lại (Re-grade) | GV, AI | GV chọn chấm lại 1 bài cụ thể (VD: bài AI bị lỗi/timeout) hoặc chấm lại toàn bộ (VD: sau khi sửa rubric) |

---

## Nhóm 4 — Review kết quả

> Sau khi AI chấm xong, GV review lại từng bài trước khi chốt điểm.

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC-09 | Xem chi tiết kết quả bài nộp | GV | Xem source code + bảng điểm từng tiêu chí + nhận xét AI + tổng điểm |
| UC-10 | Review / chỉnh điểm | GV | GV duyệt điểm AI, hoặc sửa điểm từng tiêu chí kèm lý do, hoặc override điểm tổng → chốt điểm |

---

## Nhóm 5 — Báo cáo

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC-11 | Xem bảng điểm phiên chấm | GV | Bảng tổng hợp: tên SV (tên file) × điểm từng tiêu chí + tổng, trạng thái từng bài. Thống kê TB, Min, Max, tỷ lệ đạt |
| UC-12 | Export điểm | GV | Xuất CSV / Excel: cột định danh SV = tên file (VD: `2021001`), kèm tổng điểm hoặc chi tiết từng tiêu chí + nhận xét |

---

## Nhóm 6 — Cấu hình

| ID | Tên Use Case | Actor | Mô tả |
| :--- | :--- | :--- | :--- |
| UC-13 | Cấu hình AI Provider | GV | Chọn provider (OpenAI / Gemini), nhập API key, chọn model, cấu hình timeout, test kết nối |

---

## Tổng hợp

| Nhóm | Tên nhóm | Số UC |
| :--- | :--- | :--- |
| 1 | Quản lý Rubric | 4 |
| 2 | Phiên chấm bài | 2 |
| 3 | Chấm bài | 2 |
| 4 | Review kết quả | 2 |
| 5 | Báo cáo | 2 |
| 6 | Cấu hình | 1 |

**Tổng cộng:** 14 Use Case (đã gộp 3 cặp, sửa 1 UC)
