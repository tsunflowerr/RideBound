# Defect, evidence và residual risk

## Evidence hiện có trước WP9 outcome

- Frontier WP8: 25/25 terminal, verifier independent; baseline behavioral-equivalence
  falsification pass.
- Required solution: 840/840 .NET, 0 fail/skip tại source state trước final docs.
- FleetPy/adversarial/analyzer Python: chạy bằng pinned 1.0.2, không skip.
- Burden calculator ↔ BCL-only process oracle: canonical differential + byte mutation.
- Verifier: historical default output byte-identical, manifest/checkpoint/report mutation
  bị bắt; audited solver gate bắt thiếu/sai evidence.
- Freeze v2 verifier: recompute explicit file hashes, Runner, derivative tree, scenario
  plan tree và matrix denominators.

Count cuối cùng phải được cập nhật lại sau mọi source/doc amendment và trước smoke;
không lấy con số ở file này thay cho receipt WP9.

## Residual risk không được che

1. Fixed panel chỉ có 5 independent travel-day clusters; không đủ population inference
   đã đặt ra ban đầu.
2. Full FleetPy run dài, nên crash môi trường vẫn có thể tạo incomplete bundle. Rerun
   chỉ được phép cho output không valid; valid output không được thay theo metric.
3. Full repository inventory chứng minh content state ổn định trong run, nhưng không
   chứng minh logic/statistical specification đúng; đó là vai trò của review/oracle.
4. Candidate caps vẫn là bounded approximation; loss diagnostics không phải quality
   bound trên mọi workload.
5. C1 pickup lock có thể làm service gate fail. Kết quả âm là outcome hợp lệ, không
   phải lý do đổi margin hay chọn C2 sau khi xem dữ liệu.
