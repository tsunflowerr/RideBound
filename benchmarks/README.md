# Benchmarks

Nơi lưu schema, scenario và manifest tái lập. Không đưa kết quả lớn hoặc dữ liệu
không có quyền phân phối vào Git.

- `schemas/`: JSON Schema và golden fixtures từ WP1.
- `configurations/`: named canonical policy input; test profiles không phải user
  preference hoặc production default.
- `scenarios/`: scenario nhỏ có thể version-control.
- `manifests/`: cấu hình run, seed, checksum và phiên bản artifact.
- `results/`: output cục bộ, bị `.gitignore` loại trừ.

WP4 cung cấp `configurations/wp4-rolling-cost-boundary-v1.json` như strict
boundary-test config, không phải production recommendation. Synthetic candidate-
selection microbenchmark ghi output local vào `results/`; closure summary được
version-control tại `docs/reviews/wp1-wp4-final/08-evidence-gaps-and-debugging.md`
để không biến toàn bộ result directory thành source artifact.
