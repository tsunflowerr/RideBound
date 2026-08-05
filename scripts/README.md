# Scripts

Chỉ đặt các công cụ build, replay, benchmark và phân tích có thể tái lập tại đây.
Không đặt logic thuật toán RideBound trong script.

`run-wp2-tiny-demo.ps1` chỉ publish/call cùng `RideBound.Runner`, replay transcript
source-controlled hai lần và so exact golden/hash; không cài lại B1 trong script.
Stdin process dùng UTF-8 không BOM thay vì native PowerShell pipeline để kết quả
không phụ thuộc Windows PowerShell 5 hay PowerShell 7.

`run-wp3-commitment-demo.ps1` publish/call đúng Runner `commitment` mode với named
policy config, so hai process byte-exact, certificate/promise/budget/final hashes
và restore checkpoint ở process mới cho suffix giống uninterrupted replay; script
không cài lại validator hay commitment logic và dùng cùng UTF-8 process boundary.

`run-wp4-microbenchmark.ps1` chỉ chạy project tool
`tools/RideBound.Wp4Microbenchmark`; solver/model logic vẫn nằm trong production
projects. Output là JSON machine-local, wall time chỉ mô tả và không được dùng làm
replay decision hoặc production SLA.
