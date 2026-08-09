# Risks, debt và handoff

## 1. Không còn correctness blocker đã biết cho WP6 refinement

Source audit không còn đường publish trước T3, mutable authorization mapping,
cross-run head-of-line batch hay warning gate chưa xử lý. Điều này đủ để thiết kế
common scenario/result harness trên contracts hiện tại.

## 2. Rủi ro còn lại nhưng không chặn refinement

| Risk/debt | Tác động | Cách giữ an toàn / owner tiếp theo |
|---|---|---|
| Rollout mặc định Disabled, chưa production soak | Chưa biết operational tail/SLA | Không bật Live bằng kết quả local; cần deployment plan riêng |
| SignalR không durable client receipt | Client offline có thể cần catch-up | Timeline là durable truth; không claim exactly-once |
| Frontend dedup memory bounded | Duplicate rất cũ sau eviction có thể cần projection idempotency | Consumer dùng stable IDs/projection; WP11 nếu product hóa |
| Concurrent same-key create có thể làm duplicate pre-DB Runner work | Wasted bounded capacity, không duplicate durable run | Giữ capacity/idempotency; đo trước khi thiết kế reservation optimization |
| Hardening migration từ chối outbox row null | Upgrade cần operator repair nếu có legacy synthetic data | Audit provenance rồi backfill; tuyệt đối không gán operation tùy ý |
| Local curves chỉ claim/lease mechanics | Không có end-to-end throughput/tail | WP6 đo harness overhead; production SLA ngoài claim hiện tại |
| Paired fixture nhỏ, mechanical | Không biết effect size/service tradeoff | WP8 pilot + prereg trước WP9 |
| O-002/O-003/O-004 chưa khóa | Không thể chọn budget/material threshold/margin | Chỉ khóa sau pilot, không lấy từ paper/microbenchmark |
| WP6 dataset license/normalizer chưa thiết kế | Chưa reproducible raw→scenario→metric | `RB-WP6-001` refinement-only |
| FleetPy capability/edge progress chưa proof | Chưa Layer-2 portability | WP7 executable preflight sau WP6 |

## 3. Stop conditions

- Không mở main experiment nếu raw-to-metric chưa deterministic hoặc exclusion log
  có thể sửa sau khi thấy outcome.
- Không adapter nào được reimplement RideBound decision/validator/certificate.
- Không dùng dataset không có checksum/license/provenance.
- Không gộp failure thành missing row; mọi failure/exclusion phải typed và retained.
- Không chọn numeric commitment defaults hoặc non-inferiority margin trong WP6.
- Không sửa common candidate/config allowlist riêng cho một arm sau khi xem result.

## 4. Handoff duy nhất

Ticket duy nhất được mở là `RB-WP6-001 — refinement common benchmark harness`,
trạng thái `READY`. Ticket chỉ khóa scenario/result schema, dataset normalization,
metric computation, failure/exclusion taxonomy, artifact manifest và ordered WP6
queue; chưa viết harness production và chưa chạy confirmatory experiment.

Sau khi refinement DONE, ADR mới phải chỉ định đúng một implementation ticket nhỏ
nhất `READY`. Không mở song song WP7/WP8 để né thiết kế reproducibility.

## 5. Verdict cuối

WP1–WP5 tạo một đường mechanical/correctness hợp lý và không cụt ngõ: protocol →
online state → promise/certificate → bounded solver → durable BeGo integration đã
được nối bằng exact hashes và recovery. Nhưng giá trị nghiên cứu cuối cùng vẫn chưa
được chứng minh. WP6–WP9 còn phải biến hệ thống đúng thành thí nghiệm công bằng,
tái lập và có thống kê; nếu các gate đó thất bại, verdict effectiveness phải là
negative/inconclusive thay vì sửa metric hoặc claim.
