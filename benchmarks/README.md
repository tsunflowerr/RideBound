# Benchmarks

Nơi lưu schema, scenario và manifest tái lập. Không đưa kết quả lớn hoặc dữ liệu
không có quyền phân phối vào Git.

- `schemas/`: JSON Schema và golden fixtures từ WP1.
- `configurations/`: named canonical policy input; test profiles không phải user
  preference hoặc production default.
- `scenarios/`: scenario nhỏ có thể version-control.
- `manifests/`: cấu hình run, seed, checksum và phiên bản artifact.
- `results/`: output cục bộ, bị `.gitignore` loại trừ.
