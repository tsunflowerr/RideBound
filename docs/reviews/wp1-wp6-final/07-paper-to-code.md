# Paper → code và phần cố ý không áp dụng

## Alonso-Mora et al. (2017)

Paper cho cấu trúc request–vehicle → feasible trip → RTV graph → assignment ILP và
anytime incumbent. RideBound dùng tinh thần candidate/fleet separation, feasibility
pruning và solver fallback. Không copy pre-pickup reassignment: O-001 cấm đổi xe cho
accepted incumbent. Candidate/work cap luôn có omission diagnostics; không gọi truncated
set là exact shareability graph.

## Simonetto et al. (2019)

Sparse LAP/auction và batch ngắn là hướng mở rộng tốc độ. Hiện không áp sparse filter
vào production vì chưa có oracle/loss bound/revalidation evidence. Một direction filter
có thể nhanh nhưng bỏ feasible candidate; WP4 chỉ nhận nó sau differential gate.

## Santi et al. (2014)

Shareability network giải thích cấu trúc khả năng chia sẻ và potential quy mô lớn. Nó
không phải commitment algorithm, không chứng minh RideBound stability và không được dùng
để suy mức tiết kiệm cho dataset hiện tại.

## Ackermann & Rieck (2025), multiple-plan DARP

Paper hỗ trợ plan pool, executable distinguished plan, future alternatives, compatibility
mỗi epoch và diversity/consensus hypotheses. Nó cũng cho thấy consensus/preemptive stop
không luôn tốt và overoptimization có thể làm mất flexibility. Vì vậy B5 tách pairing,
pool bounded/canonical và chỉ distinguished plan được apply; không claim B5 universal.

## Time-consistent DARP / commitment literature

Literature đã có ETA/time-consistency và dynamic insertion. RideBound không claim các
khái niệm đó mới. Phần nghiên cứu được phép là audited multi-dimensional cumulative
revision ledger + machine-checkable publication certificate dưới ranh giới đã khai báo.

## Engelhardt et al. speed-up heuristic

Direction/distance filter có thể tăng tốc kèm quality loss. Chưa áp dụng vì WP4/WP6 cần
deterministic oracle, retained-loss diagnostics và hard revalidation trước khi cắt.
Không thêm filter chỉ vì paper báo speedup ở workload khác.

## FleetPy framework/data paper

Modular request/plan/stop abstraction và Manhattan public artifact hỗ trợ boundary
dataset/normalizer/adapter. WP6 chỉ dùng source derivative cho mechanical gate. Simulator
clock, vehicle progression, traveler model và control loop vẫn thuộc WP7.

## Reproducibility/integrity sources

RFC 8493 hỗ trợ BagIt completeness/checksum; WP6 siết thêm semantic/provenance/oracle.
ACM artifact guidance và NASEM/Peng/Munafò hỗ trợ nhãn same-team, non-confirmatory và
reproducibility caveat. Unicode security guidance ảnh hưởng claim skeleton checker.

## Quy tắc chuyển paper thành code

Một ý tưởng chỉ vào production khi:

1. không vượt claim boundary;
2. có semantics cụ thể và failure mode;
3. giữ hard validator/publication gate;
4. có exact-small/differential/adversarial evidence phù hợp;
5. benchmark cùng input/work hoặc ghi rõ asymmetry;
6. kết quả âm vẫn được giữ.

Do đó closure audit không thêm thuật toán mới. H/I chứng minh harness hiện hành lặp lại,
không chứng minh một heuristic mới sẽ tốt.
