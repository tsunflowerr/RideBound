# Review cuối WP1–WP6

> Ngày chốt: 2026-08-13  
> Phạm vi: mã nguồn, ràng buộc, thuật toán, tích hợp và bằng chứng cơ học WP1–WP6  
> Verdict: WP1–WP6 đủ điều kiện đóng về **mechanical correctness và reproducibility**;
> chưa đủ điều kiện tuyên bố C1 hiệu quả hơn B1.

Đây là điểm vào dễ đọc cho toàn bộ hệ thống. Review này thay `wp1-wp5-final` làm
handoff hiện hành, nhưng không xóa các review cũ vì chúng là bằng chứng theo thời điểm.

## Đọc theo nhu cầu

1. [Kiến trúc và luồng E2E](01-kien-truc-va-luong-e2e.md)
2. [WP1–WP2: contract, state và physical baseline](02-wp1-wp2-contract-state-physical.md)
3. [WP3: promise, delta, ledger, lock và publication](03-wp3-ledger-rang-buoc-publication.md)
4. [WP4: candidate, objective, plan pool và OR-Tools](04-wp4-thuat-toan-va-solver.md)
5. [WP5: BeGo durable adapter](05-wp5-bego-durable-boundary.md)
6. [WP6: dữ liệu, benchmark, oracle và bundle](06-wp6-benchmark-harness.md)
7. [Paper → code → phần không áp dụng](07-paper-to-code.md)
8. [Bản đồ từng file code](08-ban-do-file-code.md)
9. [Bằng chứng, rủi ro và lệnh tái lập](09-bang-chung-rui-ro-tai-lap.md)

## Kết luận ngắn

- WP3 không phải một chuỗi `if/else` gắn thêm vào B1. Nó dựng lại schedule và
  promise, tính ba delta độc lập, cộng dồn vector 10 chiều không hoàn lại, áp phase/
  freeze/final locks, rồi revalidate toàn fleet trước publication.
- WP4 tối ưu trên candidate set có work budget minh bạch. Exact-small oracle kiểm
  phần đã tuyên bố exact; production không gọi bounded search là global optimum.
- WP5 không sao chép RideBound vào BeGo. BeGo giữ durable orchestration, còn cùng
  `RideBound.Runner` vẫn là authority cho decision/certificate/checkpoint.
- WP6 không chạy “benchmark đẹp”. Nó giữ failure/exclusion/resource row, tính metric
  từ raw evidence bằng production calculator và oracle độc lập, rồi khóa bundle bằng
  strict semantic verifier và machine-readable claim checker.
- Public-medium H/I trên exact source cuối đạt 8/8 mỗi process, 16/16 semantic top-level và 72/72
  semantic per-run fields giống nhau. Tất cả 8 full resource rows khác nhau hợp lệ.
  Kết quả resource cục bộ vẫn không cho claim effectiveness/SLA.

## Ranh giới sau khi đóng WP6

WP7 mới sở hữu FleetPy closed-loop adapter và simulator clock. WP8 mới được khóa
budget/threshold/non-inferiority qua pilot/preregistration. Vì vậy các câu “C1 tốt hơn
B1”, “ít trễ hơn”, “tăng hài lòng” hoặc “đạt SLA” vẫn bị cấm.
