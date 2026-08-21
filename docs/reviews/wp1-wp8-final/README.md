# Final source/logic review WP1–WP8

> Source state: pre-outcome WP9 freeze v2, 2026-08-21. Review này thay thế mọi
> câu kết luận quá mạnh trong bản nháp 2026-08-19.

## Verdict

Không còn blocker correctness/integrity đã biết trước khi mở WP9. Verdict này
không phải chứng minh hình thức: nó dựa trên kiểm kê toàn bộ file, static scan,
đọc tay các path rủi ro cao, differential/mutation, actual FleetPy và test
out-of-process. Kết quả WP9 dù dương hay âm vẫn phải giữ nguyên.

Phạm vi kiểm kê:

- `src`, `tools`, FleetPy adapter/analyzer: **187 file code**, khoảng **62.521
  dòng**; mọi file được đưa qua scan dependency/nondeterminism/float/exception/
  TODO/ordering và source inventory;
- `tests`: **102 file C#**, khoảng **26.317 dòng**, cộng Python adapter tests;
- đọc tay lại contract/hash/session, reducer/physical validation, ledger/delta/
  budget/certificate, candidate/solver/fallback, process/bundle/oracle, FleetPy
  lifecycle/verifier và toàn bộ WP8/WP9 design-analysis path.

Không tuyên bố đã chứng minh từng dòng đúng. Những vùng có tác động outcome được
ưu tiên review sâu; stream members `NotSupportedException` và unknown document
type trong codec là fail-closed có chủ đích, không phải TODO.

## Kết luận theo work package

| WP | Boundary đã đọc lại | Kết luận hiện hành |
|---|---|---|
| WP1 | strict DTO/envelope, canonical integer JSON, hash chain, retry/idempotency | Không thấy ambiguity hoặc replay-advance còn mở; parser reject duplicate/unknown/noncanonical |
| WP2 | online state, lifecycle reducer, route/frozen prefix, physical validator | Candidate không tự hợp thức hóa state; conservation và no-reassignment vẫn được chặn ở core |
| WP3 | 10D promise, three-way delta, ledger, lock/budget, certificate/checkpoint | Delta decision/exogenous/visible là ba absolute quantities, **không cộng được** nói chung; regression đã khóa điểm này |
| WP4 | bounded candidate generation, portfolio, policy, CP-SAT/fallback/evidence | Deterministic order và exact-small differential giữ nguyên; không claim global optimality ngoài solver evidence cụ thể |
| WP5 | BeGo durable boundary từ evidence lịch sử | Core không tham chiếu BeGo/EF/ASP.NET; Layer-1 claim vẫn mechanical, không phải effectiveness; không chép BeGo vào repo này |
| WP6 | plan/process/store/metric/oracle/bundle/claim | Oracle burden thực sự chạy out-of-process; PID identity và parent-chain age đã sửa; verifier vẫn fail-closed |
| WP7 | FleetPy mapping/callback/clock/plan/Runner client/verifier | Cùng versioned Runner, không force assignment/reimplement; actual 1.0.2 tests không skip |
| WP8 | experimental unit, pairing, frontier, endpoint, panel/prereg/freeze | `masterSeed` không tăng N; exact 20-cell finite panel, strict 1 pp service gate; pickup-locked và drop-earned phải tách |

## Các lỗi review tìm thấy và đã sửa

Các lỗi dưới đây đều được tìm bằng đọc logic/adversarial check, không chỉ bằng
việc tăng test count:

1. `N=62` không tồn tại trong holdout 5 ngày × 4 demand realization; solver seed
   từng bị coi sai là replicate. Đổi sang fixed panel 20 cell/5 day cluster.
2. `masterSeed` từng hard-code 7; thêm CLI nhưng giữ nó ngoài experimental-unit
   identity và chỉ dùng robustness.
3. Pairing chưa bind orientation; đổi chỗ B1/C1 lật dấu mà không lỗi. Design giờ
   bind exact arm + policy + unit + arrival denominator.
4. `DecisionInducedBurdenOracle` tồn tại nhưng chưa được gọi. Differential giờ
   chạy process riêng, so canonical bytes và bắt mutation.
5. Verifier FleetPy từng hard-code “mọi arrival được phục vụ” và 32 xe. Nó giờ
   kiểm terminal partition completed/rejected và cardinality do driver bind.
6. Ngân sách treatment-only từng chỉ là convention chưa có test trực tiếp. Sáu
   fairness regression khóa effective-policy switch và baseline falsification.
7. Run label từng mang budget, làm semantic hash không thể so baseline giữa mức.
   Label chỉ còn cell + arm; behavioral hash tách provenance khỏi hành vi.
8. `RunLevelObservation` từng bắt `visible = decision + exogenous`, sai với ba
   absolute deltas. Ràng buộc giả đã bỏ và non-additivity có regression.
9. `DisruptiveRevisionFrameCount` từng đếm publication, không đếm decision frame.
   Production/oracle giờ đếm tối đa một lần mỗi decision envelope.
10. Windows process tree so tuổi mọi child với root thay vì immediate parent, có
    thể nhận nhầm PID tái sử dụng. Parent chain giờ kiểm identity từng cạnh.
11. Analyzer WP9 có arm IDs nhưng không bind bundle với execution-plan/label/
    scenario; bundle hợp lệ đặt nhầm có thể đảo dấu. Ba lớp binding và mutation
    test đã thêm trước outcome.
12. HEAD/`git status` không phát hiện sửa nội dung file vốn đã dirty. Matrix và
    preflight giờ bind content SHA-256 của mọi file Git-visible + HEAD trước/sau
    run; resume cũng phải cùng inventory.
13. `cellId/jobId` chưa path-safe. Frozen identifier grammar giờ fail-closed trước
    mọi filesystem resolution.

## Giới hạn bắt buộc giữ trong báo cáo

- WP9 chỉ kết luận trên finite panel 20 cell; không population p-value/NI CI.
- Pickup-ETA reduction của C1 là phần **do định nghĩa lock**; chỉ drop-ETA không
  bị lock là phần “earned”. Pilot cho tỷ lệ xấp xỉ 18%/82%, không được mặc định
  tỷ lệ đó cho confirmatory.
- Giá dịch vụ phải tách lock price (B1 → C1 unbounded) khỏi budget price
  (C1 unbounded → C1 tight).
- C2, seed 19 và unbounded là descriptive robustness, không cứu primary gate.
- Budget/promise là synthetic policy overlay; không có nhãn satisfaction/fairness
  nhân khẩu học và không được claim user benefit.
- Dynamic insertion, ETA/time consistency, reassignment, shareability,
  least-commitment và satisfaction đều là prior art/claim bị cấm theo `docs/03`.

Chi tiết determinism/fairness ở `01`, walkthrough theo WP ở `02`, defect/evidence
ở `03`.
