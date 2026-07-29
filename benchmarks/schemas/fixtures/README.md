# Contract fixtures

Các fixture trong thư mục này là UTF-8 không BOM và dùng LF trong source.
`RideBound.Contracts.Tests` tìm fixture từ repository root, không phụ thuộc
current working directory hoặc path separator của hệ điều hành.

Mỗi fixture protocol sau smoke harness phải khai báo support level theo
`docs/06-event-contract-and-determinism.md`:

- `schema-only`;
- `runner-executable`;
- `future-behavior`.

Golden canonical bytes/hash là source-controlled oracle. Test không được tự cập
nhật expected vector khi chạy.

Thư mục hiện có:

- `compatibility/`: hành vi version/error chính xác;
- `hello/`: capability được cung cấp và kết quả lựa chọn;
- `initialize/`: manifest bất biến và danh tính state ban đầu;
- `protocol/`: boundary envelope chung;
- `canonical/`: vector canonical byte chính xác;
- `hash/`: vector manifest/state/decision SHA-256 có input và expected hash;
- `golden/required/`: đúng 10 scenario bắt buộc; 9 scenario
  `future-behavior`, riêng duplicate là `runner-executable`;
- `runner/`: full tiny NDJSON transcript, exact output và final decision hash;
- `harness/`: dữ liệu smoke cho fixture loader.

Q1 chỉ tính `golden/required/09-duplicate-event-idempotent` và transcript trong
`runner/` là behavior executable. Các scenario accept/reject, budget, incident
và checkpoint chỉ được kiểm cấu trúc cho tới WP2/WP3.
