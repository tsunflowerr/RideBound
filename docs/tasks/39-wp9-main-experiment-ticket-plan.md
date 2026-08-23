# WP9 main experiment — ordered ticket plan

> Nguồn khóa hiện hành: preregistration `H0=c653c3ce…b1fc2`, node-cap amendment
> `184bf923…fddf9`, analysis-integrity amendment `db1731e6…f1d70`,
> Runner-repin amendment `d1d9465e…ac90e`, capacity-panel amendment `wp8-011d`,
> receipt lịch sử `H2=97af95cf…d3049`/`H3=d028eae4…dd14e`/`H4=2f7e6bf3…a32dd`,
> **current receipt `H6=84f6eff3…3dee2`** (`freeze-receipt-v5.json`, schema 5.0.0).
>
> `H5` đã bị vô hiệu bởi ADR-047. Mọi bundle chạy dưới `H5` — gồm Panel A 60/60 ở
> `E:\RideBoundData\wp9\confirmatory-h5-panela` và Panel B 6/40 — **không** được
> trộn vào estimand và không được trích dẫn làm outcome. Giữ nguyên byte làm bằng
> chứng vận hành.
>
> `H4` đã hết hiệu lực vận hành: ADR-045 đổi Runner và hai analyzer, `wp8-011d`
> thêm Panel B. Cả hai đều outcome-bearing. Mọi run trước `H5` — kể cả smoke hỏng
> `failed-smoke-h4-20260821` và diagnostic `diagnostic-adr045-20260822` — **không**
> được trộn vào bất kỳ estimand nào.

| Ticket | Nội dung | Điều kiện đóng |
|---|---|---|
| `RB-WP9-001` **Done** | Enrollment/freeze seal | 20 unit; H4 verifier PASS tại thời điểm đó; no confirmatory outcome |
| `RB-WP9-002a` **Done** | Repin freeze sau ADR-045/046/047 + `wp8-011d` | `H6` bind file hash + 5 tree seal (derivative A, derivative B, scenario plan, Runner tree, adapter package); verifier PASS; mutation bị từ chối |
| `RB-WP9-002b` **Done** | Pinned Runner và audited smoke trên `H6` | Cả hai panel có pair audited dưới H6 và sau đó toàn bộ 100 bundle qua independent verifier. H6 evidence v1.0 chưa serialize breach; probe hậu-kết-quả v1.1 trên cùng cell lỗi lịch sử chứng minh 43 epoch breach thật. Không hồi tố 43 observation vào H6 |
| `RB-WP9-003` **Done** | Layer-1 mechanical paired evidence | 8/8 PASS, external verify; không nâng thành Layer-2 effectiveness |
| `RB-WP9-004` **Done** | Chạy lại Layer-2 Panel A rồi Panel B dưới `H6` | Panel A 40 primary + 20 robustness và Panel B 40 primary hoàn tất; không dùng lại H5; terminal receipts PASS và inventory bất biến |
| `RB-WP9-005` **Done** | Exact panel analysis | Hai canonical output đủ 20 paired row: Panel A `−7.1296 pp`, Panel B `−4.9074 pp`; cả hai service gate FAIL, burden gate PASS; locked/earned và precision được công bố |
| `RB-WP9-006` **Done** | Robustness/ablation | C1 unbounded, C2 loose, seed 19 chỉ trên Panel A; `confirmatoryGate:null`, seed không tăng N, không cứu primary |
| `RB-WP9-007` **Done** | Reproducibility bundle | Frozen verifier kiểm 100/100 bundle; bốn falsification condition PASS; repeat deterministic; 5 mutation bị từ chối; compact receipt source-controlled |
| `RB-WP9-008` **Done** | Source/claim closure | Negative result được giữ nguyên, burden không bị oversell, verdict hữu hạn và có điều kiện theo 4/8 xe; report/ADR/status/traceability đồng bộ |
| `RB-WP9-009` **Done** | Breach evidence + ledger bridging | Evidence v1.1 tương thích ngược; exogenous ledger không incident giả/không charge budget; checkpoint strict; real FleetPy probe có 43/43 evidence/ledger breach, chỉ dùng làm post-outcome mechanism evidence |

Không ticket nào được sửa prereg payload, bốn amendment, hai grid, hai execution
plan, analysis/robustness manifest hoặc Runner artifact đã repin. Program được bind
bởi `H6`; bug ảnh hưởng outcome sau smoke phải invalidate affected grid theo
preregistration, không vá lẻ.

## Thứ tự bắt buộc và quy tắc frozen-source

`002a` → `002b` → `004` → `005`/`006` → `007` → `008`. `009` sau `004`.

Trong lúc bất kỳ ma trận nào đang chạy, **không được sửa một byte nào** trong cây
Git-visible: `actual_fleetpy_medium_preflight.py` so `repositoryInventorySha256`
trước và sau mỗi run và fail closed nếu lệch. Điều này đã bắt được một lần trong
diagnostic 2026-08-22 khi tài liệu bị sửa giữa chừng.

## Ràng buộc claim đã khóa trước outcome

- Kết luận là **có điều kiện theo năng lực**: chi phí dịch vụ của cam kết bằng 0 ở
  năng lực dư và dương thật ở năng lực căng, phát biểu tại đúng hai điểm đã đo.
  Luận điểm phổ quát "cơ chế cam kết rẻ" bị cấm kể cả khi Panel A đạt gate.
- Panel A và Panel B khác `scenarioHash` nên so sánh giữa hai panel là
  **between-panel diagnostic**, không phải paired test.
- Chỉ kết luận trên finite panel; không population p-value/NI CI. Sàn sign-flip là
  `1/2^5 = 0,03125` vì panel chỉ có 5 travel realization.
- Exogenous burden là diagnostic mô tả, **không** phải negative control
  (`wp8-010`). Falsification dùng identity chuỗi demand+travel.
- Phần pickup-ETA của C1 là definitional theo lock; tỷ lệ pilot 18%/82% không
  được mặc định cho confirmatory.
- H6 bundle dùng evidence v1.0 nên breach count **không đo được trong estimand
  confirmatory**. Con số 43 chỉ đến từ probe hậu-kết-quả v1.1 và luôn phải gắn nhãn
  mechanism verification, không được trộn vào hoặc cứu gate H6.

## Closure 2026-08-23

WP9 đóng với kết quả âm. Báo cáo chính ở
`docs/benchmarking/wp9-confirmatory-result-2026-08-23.md`; reproducibility ở
`docs/benchmarking/wp9-reproducibility-evidence-2026-08-23.md`; bridge evidence ở
`docs/benchmarking/wp9-009-breach-evidence-2026-08-23.md`. Bước kế tiếp là WP10
cross-system; không tinh chỉnh lại H6 hay diễn giải robustness như rescue.
