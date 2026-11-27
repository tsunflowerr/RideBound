# RideBound

RideBound là bộ điều phối xe đi chung trực tuyến. Khi điều phối lại, chế độ C1 theo dõi tổng mức thay đổi do quyết định gây ra đối với lời hứa của từng khách. Runner nhận sự kiện và trả quyết định qua NDJSON.

## Cấu trúc

- [`src/core/`](src/core/) chứa solution .NET, các project C#, Runner và bộ test.
- [`src/core/protocol/`](src/core/protocol/) chứa JSON Schema v1, fixture giao thức và kịch bản kiểm thử.
- [`references/`](references/README.md) liệt kê công trình, dữ liệu và phần mềm được trích dẫn, kèm giấy phép của các PDF.

## Kiểm tra và chạy

Cài .NET SDK theo [`src/core/global.json`](src/core/global.json), rồi chạy các lệnh sau từ thư mục gốc.

```sh
cd src/core
dotnet restore RideBound.slnx
dotnet test RideBound.slnx
dotnet run --project RideBound.Runner -- --mode online
```

Runner đọc các khung NDJSON từ stdin. Ví dụ về đầu vào và đầu ra nằm trong [kịch bản nhỏ](src/core/protocol/scenarios/wp2-tiny/).

Để chạy policy C1 với cấu hình mẫu, dùng lệnh sau từ `src/core/`.

```sh
dotnet run --project RideBound.Runner -- --mode online --c1-solver-config protocol/fixtures/runner/ridebound-hard-vector.config.json
```
