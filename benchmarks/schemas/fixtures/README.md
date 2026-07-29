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
