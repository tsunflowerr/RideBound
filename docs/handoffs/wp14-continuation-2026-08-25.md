# RideBound handoff — WP14 continuation

> Cập nhật cuối 2026-08-26: `RB-WP14-009 Closed — FAIL CLOSED`. B1 valid, C1
> partial thiếu manifest; recovery không re-execute, partial được giữ, không retry/
> replacement. `RB-WP14-010..014` không được authorize dưới freeze v1. Xem report
> `benchmarking/wp14-009-paired-dry-run-resource-gate-2026-08-26.md`.

> Ngày: 2026-08-25
> Checkpoint lịch sử trước freeze: `WP13 COMPLETE`; `RB-WP14-001..005 DONE`;
> `RB-WP14-006/007 DEFERRED`
> H6 / WP10 / WP13 evidence: **immutable**
> Đối tượng đọc: agent tiếp theo (Codex) — đọc hết file này trước khi gõ dòng code đầu tiên

---

## 0. Đọc gì trước

Bắt buộc, theo thứ tự:

1. [`AGENTS.md`](../../AGENTS.md) — verification baseline và ranh giới repository
2. [`docs/18-status-and-decision-log.md`](../18-status-and-decision-log.md) — live source of truth, ADR-001..068
3. [`docs/tasks/43-wp14-exploratory-ablation-refinement.md`](../tasks/43-wp14-exploratory-ablation-refinement.md) — factor design và ba ràng buộc cứng
4. [`docs/tasks/44-wp14-exploratory-ablation-ticket-plan.md`](../tasks/44-wp14-exploratory-ablation-ticket-plan.md) — ordered queue
5. [`docs/reviews/wp1-wp13-optimization-and-fairness/README.md`](../reviews/wp1-wp13-optimization-and-fairness/README.md) — vì sao benchmark thấp, đo từ raw evidence
6. [`docs/research/wp14-ablation-pareto-full-pdf-evidence-2026-08-24.md`](../research/wp14-ablation-pareto-full-pdf-evidence-2026-08-24.md) — literature đã đọc full-text

---

## 1. Bối cảnh nghiên cứu

### 1.1 Bài toán

RideBound thử nghiệm một cơ chế **hard commitment** cho ride-pooling: khi hệ thống
đã hứa ETA với khách, operator tự ràng buộc mình không được tuỳ tiện sửa lời hứa đó.
Câu hỏi nghiên cứu: giữ lời hứa có tốn bao nhiêu dịch vụ?

- Baseline **B1** `rolling-cost`: tối ưu lại tự do, chỉ tối đa hoá accepted count rồi
  tối thiểu operational cost.
- Treatment **C1** `ridebound-hard-vector`: cùng generator, cùng solver, nhưng
  candidate nào vi phạm commitment thì bị **loại khỏi tập chọn** trước khi solver chạy.

### 1.2 Kết quả xác nhận H6 (đã đóng băng, không được sửa)

| Panel | Xe | Arrivals/arm | B1 completed | C1 completed | Δ service | Gate |
|---|---:|---:|---:|---:|---:|---|
| A | 8 | 2.160 | 1.735 | 1.581 | `−154` = `−7,1296 pp` | **FAIL** |
| B | 4 | 2.160 | 966 | 860 | `−106` = `−4,9074 pp` | **FAIL** |

Margin non-inferiority đã prereg là `−1,00 pp`. Burden gate PASS (giảm 99,83% /
99,23%) nhưng **không cứu được** service gate.

Đây là **negative result** và nó là kết quả hợp lệ. Toàn bộ WP13/WP14 tồn tại để
hiểu vì sao, **không** để làm cho nó đẹp hơn.

---

## 2. Kết quả hiện tại: vì sao benchmark thấp

Tất cả số dưới đây đo lại từ 80 bundle E1 đã đóng băng bằng
[`wp14_mechanism_probe.py`](../../simulators/fleetpy-ridebound/wp14_mechanism_probe.py),
receipt [`wp14-001-mechanism-probe-v1-summary.json`](../benchmarking/evidence/wp14-001-mechanism-probe-v1-summary.json).
Probe tái lập **đúng 10/10** con số H6 đã công bố trước khi đưa ra bất kỳ phát hiện mới nào.

### 2.1 Toàn bộ mất mát quy về đúng hai con số cấu hình

| | Panel A C1 | Panel B C1 | B1 (cả hai panel) |
|---|---:|---:|---:|
| commitment prune | 940 | 583 | **0** |
| … `drop_eta_total_ms` budget 30 s | 780 | 491 | 0 |
| … `pickup_eta_ms` final-confirmation lock | 160 | 92 | 0 |
| vehicle choice set bị gate làm rỗng | 534 | 339 | 0 |
| request bị chặn ngay lập tức | 143 | 212 | 0 |

**8/10 dimension của commitment vector không sinh một witness nào** trong 44.156
decision. `vehicle_switch_count` và hai `*_stop_switch_count` có `hardLimit = 0`
nhưng không bao giờ bị vi phạm vì O-001 đã cấm reassignment ở tầng domain cho **cả
hai arm**.

### 2.2 Nới budget không cứu được — phân phối lưỡng cực

Tiêu thụ tích luỹ `drop_eta_total_ms` trên arm **không bị ràng buộc** (B1 Panel A,
n = 1.735):

| Ngưỡng | Số request | % |
|---|---:|---:|
| bằng 0 | 1.332 | 76,8% |
| `> 30.000 ms` | 369 | 21,3% |
| `> 60.000 ms` | 341 | 19,7% |
| `> 120.000 ms` | 230 | 13,3% |
| `> 300.000 ms` | 48 | 2,8% |

p90 = 154.821 ms, p95 = 234.222 ms, max = 573.491 ms.

Hoặc không đụng gì, hoặc đụng rất lớn. **Gần như không có khối lượng giữa 0 và 30 s.**
Đi từ 30 s lên 60 s chỉ gỡ 1,6 pp; lên 300 s vẫn còn 2,8% bị ràng buộc.

Hệ quả cho nghiên cứu: đánh đổi thật **không phải** "sửa ETA một chút để phục vụ thêm
khách", mà là **"nhận thêm một khách, đổi lại đẩy drop-ETA của một khách đang chờ lùi
2,5–9,5 phút"**. Hard commitment là luật từ chối đánh đổi đó. Đây là câu chuyện khoa
học đúng và nó thú vị hơn "budget quá chặt" nhiều.

### 2.3 Ranking của C1 gần như bất hoạt

C1 chèn 11 lexicographic level giữa `accepted-request-count` và `operational-cost`.
Đo trong tập option solver thực nhận (Panel A, 3.688 choice set có ≥2 option):

- `worst-hard-utilization-ppm`: **0,000%** phân biệt — hằng số `1000000` ở
  **13.004/13.004** decision. `CalculateWorstUtilization` ép nó lên trần vì có
  `hardLimit = 0` trên các dimension luôn thoả.
- 7/10 `revision:*` level: **0,000%**.
- Chỉ `drop_eta_total_ms` (2,09%), `material_eta_revision_count` (2,09%),
  `pre_pickup_inserted_stop_count` (1,19%) từng phân biệt.
- Xếp lại theo thứ tự B1 trên **cùng** tập eligible: lựa chọn cục bộ chỉ đổi ở
  **28/3.794 = 0,738%** choice set.

**C1 không phải "B1 + ranking commitment-aware". Nó là "B1 áp lên tập option đã bị
gate cắt".** Bài báo không được mô tả C1 như xếp hạng theo mức tiêu thụ commitment.

### 2.4 "Giảm burden 99,83%" là attributed, không phải rider cảm nhận

| | Panel A B1 | Panel A C1 | giảm |
|---|---:|---:|---:|
| attributed (metric gate) | 74.443.002 ms | 128.020 ms | **99,83%** |
| experienced (rider thấy) | 83.576.558 ms | 9.322.567 ms | **88,85%** |
| rider có promise từng đổi | 1.734/1.735 | **1.581/1.581** | — |

Panel B: attributed −99,23%, experienced −91,43%.

100% rider ở **cả hai** arm đều thấy ETA của mình đổi ít nhất một lần. Claim được
phép là "operator không bao giờ chủ động đổi ETA của bạn", **không phải** "ETA của
bạn ổn định".

---

## 3. Ưu điểm và nhược điểm hiện tại

### 3.1 Ưu điểm

| Điểm | Bằng chứng |
|---|---|
| Dữ liệu thật, không chế biến | TLC 2018 qua FleetPy Zenodo DOI `10.5281/zenodo.15187906`; **một mã loại trừ duy nhất** `source.outside-window`; chọn mẫu HMAC tất định |
| So sánh công bằng | Config hai arm giống hệt trừ `policyId`; cùng seed 7, cùng adapter OR-Tools; B1 có **0** commitment prune đo được |
| Generation không thiên vị | WP13-007/011: generated set exact-equal 40/40 pair; cap **không bao giờ** chạm (0/104.672 vehicle-epoch) |
| Exogenous đối xứng | Drift giữa hai arm chênh < 1% ở cả pickup lẫn drop |
| Solver đúng nghĩa lexicographic | Multi-pass CP-SAT có optimum fixing thật, không phải weighted sum giả |
| Gate fail-closed | Mất hết option kể cả no-op ⇒ dừng run, không lặng lẽ trả no-op |
| Provenance chặt | 62 file source pin theo freeze receipt; verifier độc lập; 31-mutant falsification |

### 3.2 Nhược điểm và rủi ro

| Vấn đề | Mức | Trạng thái |
|---|---|---|
| 97,40% pass CP-SAT là thừa (C1), 94,40% (B1) | Hiệu năng | Đã sửa ở `002`, **opt-in mặc định tắt** |
| Attribution prune phụ thuộc thứ tự request | Đo lường | Đã sửa ở `003`, **profile riêng** |
| Panel development chỉ 2 ngày | Thiết kế | Giới hạn dữ liệu, đã ghi rõ |
| B1 yếu hơn baseline chuẩn literature | Claim | Xem 3.3 |
| Không có rebalancing ở cả hai arm | Claim | Không so tuyệt đối với literature |
| `wp14_freeze/run_matrix/frontier_analyze` **chưa có test** | Chất lượng | **Việc của bạn** — xem §6 |

### 3.3 Điểm bất đối xứng duy nhất — và nó nghiêng về phía bất lợi cho chúng ta

`CommitmentLockEvaluator` hard-code `activeLocks = PromiseLock.Vehicle` rule
`accepted_assignment`, **không phụ thuộc policy**, nên áp cho **cả B1**. Kết hợp
O-001/ADR-018: B1 cũng không được đổi xe cho request đã accept.

Baseline chuẩn thì được. Alonso-Mora et al. (đã đọc full-text):

> "A request might be rematched to a different vehicle in subsequent iterations as
> long as its waiting time does not increase and until it is picked up by some vehicle."

Nên `−7,1296 pp` và `−4,9074 pp` là **cận dưới** của cái giá so với baseline
reassignment đầy đủ, không phải cận trên. Không có chỗ nào làm treatment trông tốt
hơn thực tế; chỗ lệch duy nhất làm nó trông **đỡ tệ hơn** thực tế.

**Tuyệt đối không sửa O-001 để "làm đẹp" số.** Nếu muốn baseline mạnh hơn thì phải
là một ADR riêng, đo cả hai, và báo cả hai.

---

## 4. Mục tiêu

### 4.1 Mục tiêu khoa học của WP14

Trả lời đúng một câu: **có tồn tại cấu hình commitment nào giữ được phần lớn lợi ích
burden mà mất dịch vụ ít hơn hẳn không?**

Đầu ra là một **frontier hai trục** `(service, burden)` có nhãn cấu hình. Không phải
một cấu hình "thắng". Không có scalar xếp hạng hậu outcome.

### 4.2 Điều gì làm bài báo cáo *thật sự* tốt

Không phải một con số đẹp. Bài báo mạnh ở đây gồm bốn thứ:

1. **Negative result H6 giữ nguyên và được trình bày thẳng.** Đây là đóng góp chính:
   hard commitment trên panel này tốn 7,13 pp. Rất ít bài dám báo cái này sạch.
2. **Chẩn đoán cơ chế chính xác đến mức hai con số cấu hình**, có mutation test, có
   verifier độc lập. Đây là phần WP13 + probe đã làm.
3. **Frontier phát triển trên dữ liệu rời hẳn**, chứng minh được không leakage.
4. **Bốn phát hiện âm tính về chính thiết kế của mình** — level hằng số, ranking bất
   hoạt, attributed vs experienced, baseline yếu hơn chuẩn. Tự tìm ra và tự báo là
   thứ làm reviewer tin phần còn lại.

Nếu frontier cho thấy **không có** cấu hình nào tốt hơn đáng kể, đó **vẫn là kết quả
tốt** cho bài báo: nó nói rằng cái giá của hard commitment là cấu trúc, không phải
lỗi tham số. Đừng ép frontier phải đẹp.

---

## 5. Ràng buộc bất di bất dịch — vi phạm là hỏng cả nghiên cứu

| # | Ràng buộc | Cơ chế kiểm |
|---|---|---|
| C1 | **Không dùng số đo H6/E1 để CHỌN mức factor.** Giải thích quá khứ thì được; chọn tương lai thì không | `wp14_development_panel_audit.py`, 7 trục, 0 giao |
| C2 | **Không sửa, ghi, move, xoá** bất kỳ raw root H6/E1 nào | `forbiddenRoots` trong freeze; probe/analyzer chỉ đọc |
| C3 | **Không re-freeze E1, không backfill H6** | `source-divergence-v1.json` + 6 regression test |
| C4 | Frontier hai trục, **không scalar ranking hậu outcome** | `wp14_frontier_analyze.py` không sinh điểm số |
| C5 | Báo **đuôi per-rider** cạnh tổng fleet | Một arm có thể giảm tổng bằng cách dồn hết lên vài rider |
| C6 | Mọi artifact thành công **exclusive-create** sau khi verify xong | `write_exclusive()` |
| C7 | Không claim causal, không CI/p-value population | claim boundary trong mọi receipt |
| C8 | Mỗi ticket Done chỉ mở **đúng một** ticket kế tiếp | `tasks/44` |

### Ba cái bẫy đã sập trong phiên trước — đừng sập lại

**Bẫy 1: evidence nằm TRONG decision hash.**
`DecisionPayloadCodec.Encode(shell, hashProjection: true)` ghi `executionEvidence`
**trước** khi bỏ `decisionHash`. Evidence chứa `consumedDeterministicTimeMicros`.
Nên **mọi** thay đổi làm đổi số đo solver đều đổi `decisionHash`.
⇒ Mọi thay đổi loại này phải là **cờ/profile mới, mặc định tắt**. Không bao giờ nới
nghĩa của cờ/profile cũ.

**Bẫy 2: nới profile cũ phá freeze chain E1.**
Bản đầu của `003` suy cờ thu thập witness từ `retained-portfolio-v1` — đúng profile
mà hai config E1 đã đóng băng khai báo. Chạy lại E1 sẽ cho evidence khác.
**Không test nào bắt được** vì không có test nào replay E1. Đã sửa bằng profile riêng
`retained-portfolio-full-witness-v1` + regression đọc thẳng hai file config E1.

**Bẫy 3: `git show HEAD:` là binding trôi.**
`wp13_full_audit` bind verifier lịch sử qua `HEAD`; một commit làm HEAD dịch là fail.
Đã pin sang commit `2d6791fb916e89850d9ec2778285142943a27ee6`.

---

## 6. Trạng thái chính xác lúc bàn giao

### 6.1 Ticket

| Ticket | Trạng thái | Báo cáo |
|---|---|---|
| RB-WP14-001 refinement | Done | ADR-066, `tasks/43` |
| RB-WP14-002 constant-level skip | Done | [`wp14-002`](../benchmarking/wp14-002-constant-level-skip-2026-08-24.md) |
| RB-WP14-003 full witness set | Done | [`wp14-003`](../benchmarking/wp14-003-full-witness-set-2026-08-24.md) |
| RB-WP14-004 development panel | Done | [`wp14-004`](../benchmarking/wp14-004-development-panel-2026-08-24.md) |
| RB-WP14-005 factor F1/F2 | Done | `tasks/44` §5 |
| RB-WP14-006 F3 penalty band, F4 objective order | **Deferred** | quyết định của chủ nghiên cứu 2026-08-25 |
| RB-WP14-007 F5 hold, F6 distributional | **Deferred** | như trên |
| RB-WP14-008 freeze manifest | **Done** | [`wp14-008`](../benchmarking/wp14-008-development-ablation-freeze-2026-08-26.md) |
| **RB-WP14-009 dry-run + resource envelope** | **Closed — FAIL CLOSED** | [`wp14-009`](../benchmarking/wp14-009-paired-dry-run-resource-gate-2026-08-26.md) |
| RB-WP14-010 execute matrix | Not authorized under freeze v1 | — |
| RB-WP14-011 verifier + mutation | Not authorized under freeze v1 | — |
| RB-WP14-012 frontier report | Not authorized under freeze v1 | — |
| RB-WP14-013 audit | Not authorized under freeze v1 | — |
| RB-WP14-014 closure + WP15 decision | Not authorized under freeze v1 | — |

Hoãn F3–F6 là **hoãn, không huỷ**. Nếu `012` cho thấy frontier từ F1/F2 + budget
sweep không đạt, mở lại chúng. Chúng **chưa** bị loại bằng bằng chứng như bốn factor
ở `tasks/43` §3.

### 6.2 Baseline đã verify lúc bàn giao

- `dotnet test RideBound.slnx`: **908/908**, zero skip (trên máy rảnh — xem cảnh báo
  ngay dưới)
- pinned CPython/FleetPy: **225/225**, zero skip
- `dotnet format --verify-no-changes`: pass
- Markdown link/fence, JSON parse, `git diff --check`: pass

#### Một test **mong manh** bạn phải biết trước

`PublicDerivativeMechanicalDrainConversationTests.Exact_runner_mechanically_drains_all_medium_public_requests`
có ceiling cứng `CpuTimeLimitMs: 120_000` (dòng 125 của file test). Nó chạy một
medium public drain thật qua published Runner.

Quan sát trong phiên bàn giao:

| Điều kiện | Kết quả |
|---|---|
| Máy bận (build server sống, nhiều tiến trình) | **FAIL** `resource.cpu-time-exceeded`, 2 m 7 s |
| Máy rảnh (`dotnet build-server shutdown` trước) | PASS, 1 m 22 s |

WP13-002 đã ghi nhận đúng lỗi này ở `120.062/120.000 ms` — biên chỉ 0,05%.

Đây **không phải** regression của WP14: cùng bộ code này đã pass 908/908 trước đó
trong cùng phiên. Nhưng nó có hệ quả vận hành trực tiếp:

> **Không chạy `dotnet test RideBound.slnx` trong lúc matrix WP14 đang chạy.**
> Matrix ở parallelism 4 sẽ bão hoà máy và test này sẽ fail giả. Chạy baseline
> **trước khi** phóng matrix và **sau khi** matrix xong.

Nếu gặp fail: `dotnet build-server shutdown`, chờ máy rảnh, chạy lại **đúng test đó
một mình**. Pass ⇒ ghi là contention, kèm cả hai số đo. **Không** nới ceiling.

Lệnh chạy Python (bắt buộc từ thư mục `tests/`, nếu chạy từ adapter root sẽ báo
0 test và **đó không phải baseline hợp lệ**):

```powershell
$env:PYTHONDONTWRITEBYTECODE='1'
$env:RIDEBOUND_FLEETPY_ROOT='E:\RideBoundData\wp7\FleetPy-1.0.2'
cd E:\Code\RideBound\simulators\fleetpy-ridebound\tests
& 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe' -B -m unittest discover -s . -t . -p 'test_*.py'
```

### 6.3 Working tree

**Chưa commit gì** theo yêu cầu của chủ repository. HEAD là `38d517d` (commit của
chủ repo lúc 23:07 ngày 24-08, đã gom WP13 + review). 51 đường dẫn thay đổi/mới.

### 6.4 Tài sản WP14 đã có

Code:

| File | Có test? |
|---|---|
| `simulators/fleetpy-ridebound/wp14_mechanism_probe.py` | 7 test |
| `simulators/fleetpy-ridebound/wp14_development_panel_audit.py` | 7 test |
| `simulators/fleetpy-ridebound/wp14_freeze.py` | **CHƯA** |
| `simulators/fleetpy-ridebound/wp14_run_matrix.py` | **CHƯA** |
| `simulators/fleetpy-ridebound/wp14_frontier_analyze.py` | **CHƯA** |

Config: 9 factor level `benchmarks/configurations/wp14-c1-*.json`, 2 arm config
`benchmarks/configurations/wp14-development-*-v1.json` (đã bật cả hai opt-in).

Data: 16 cell fixture `benchmarks/fixtures/wp6/public/fleetpy-manhattan-v1/wp14-development-panel-v1/`
(67.416.839 byte), 16 driver + grid `benchmarks/scenarios/wp14-development/`.

Runner đã publish: `E:\RideBoundData\wp14\runner-v1` (Release, win-x64).

**Chưa tồn tại**: freeze receipt, bất kỳ bundle kết quả nào. Dry-run bị giết cùng
phiên trước và đã dọn sạch.

---

## 7. Envelope tài nguyên — đo từ E1 thật

E1 Panel A, 40 job thật đã chạy:

| | median | min | max |
|---|---:|---:|---:|
| wall/job | 971.998 ms ≈ **16,2 phút** | 490.822 ms | 1.514.349 ms |
| transcript/job | 97,5 MB | 65,3 MB | 119,3 MB |
| tổng 40 job | 3,82 GB | | |

WP14 có **160 job** (16 cell × 10 arm). Ngoại suy tuyến tính:

- **~15,6 GB** dữ liệu
- **~43 giờ** tuần tự; **~11 giờ** ở parallelism 4

Đĩa `E:` phải còn ≥ 25 GB trước khi chạy. **Không** claim WP14-002 làm nhanh hơn cho
tới khi đo — `009` phải đo thật.

---

## 8. Việc phải làm tiếp, theo đúng thứ tự

### RB-WP14-008 — Freeze manifest

**Done 2026-08-26.** Receipt 101.719 byte SHA `1ce26ff0…37a55`, exact 16 cell,
10 arm, 160 job, 46 repository files cùng source/runtime/tree seals; canonical verify
pass. Required .NET 908/908, pinned Python 242/242, targeted 17/17, zero skip.
Không chạy lại write mode và không sửa file đã bind. Xem report `wp14-008`.

### RB-WP14-009 — Dry-run và resource envelope

**Kết quả 2026-08-26: FAIL CLOSED. Không chạy lại lệnh bên dưới.** B1 hoàn tất và
được verify độc lập; C1 partial thiếu manifest. Recovery chỉ reuse-verify B1, reject
C1 và ghi summary 1/1; không re-execute. Theo freeze v1, giữ partial, không retry/
replacement và không chạy `010`. Lệnh dưới đây chỉ còn là provenance của attempt.

```bash
"E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe" -B simulators/fleetpy-ridebound/wp14_run_matrix.py \
  --repository "E:/Code/RideBound" \
  --freeze benchmarks/scenarios/wp14-development/freeze-receipt-v1.json \
  --output-root "E:/RideBoundData/wp14/development-ablation" \
  --forbidden-root "E:/RideBoundData/wp9/confirmatory-h6-panela" \
  --forbidden-root "E:/RideBoundData/wp9/confirmatory-h6-panelb" \
  --forbidden-root "E:/RideBoundData/wp13/e1-retained-portfolio-panel-a" \
  --forbidden-root "E:/RideBoundData/wp13/e1-retained-portfolio-panel-b" \
  --fleetpy-root "E:/RideBoundData/wp7/FleetPy-1.0.2" \
  --runner-root "E:/RideBoundData/wp14/runner-v1" \
  --python "E:/RideBoundData/wp7/envs/fleetpy-1.0.2/python.exe" \
  --dotnet "C:/Program Files/dotnet/dotnet.exe" \
  --development-panel-audit "E:/RideBoundData/wp14/development-panel-audit-v1.json" \
  --resource-planning-evidence "E:/RideBoundData/wp13/e1-retained-portfolio-inventory-v1.json" \
  --parallelism 1 \
  --job w14-d20181112-s10-r1-w08-b1-ref-s7 \
  --job w14-d20181112-s10-r1-w08-c1-h6ref-s7 \
  --summary "E:/RideBoundData/wp14/dryrun-summary-v1.json"
```

**Cảnh báo quan trọng**: ước lượng bảo thủ từ E1 là khoảng 16 phút/job. Chạy trong
một PTY dài hạn, theo dõi session định kỳ và không khởi động workload nặng khác
song song. Timeout của runner vẫn là nguồn quyết định fail-closed.

Không giảm cell/arm hoặc re-freeze để thay failure receipt v1. Một successor chỉ
được mở bằng authorization và protocol/ADR mới, độc lập với outcome của attempt này.

Ở bước này chỉ được đọc **tài nguyên**. **Không** đọc completion/burden của hai job
đó trước khi freeze đã xong — freeze đã xong ở `008` nên thực ra an toàn, nhưng vẫn
không dùng chúng để đổi factor level.

### RB-WP14-010 — Chạy matrix

**Không được authorize dưới freeze v1; không chạy lệnh này.** Nội dung dưới đây chỉ
là kế hoạch lịch sử trước khi resource gate fail.

Script đã hỗ trợ resume: bundle đã tồn tại và verify được thì `reusedVerified`, sai
thì fail. Nên có thể dừng/tiếp an toàn.

**Done khi**: `completed=160 failed=0`, mỗi bundle qua
`actual_fleetpy_medium_verify.py --include-behavioral-hash --require-audited-solver-evidence`.

### RB-WP14-011 — Verifier độc lập và mutation matrix

Theo đúng khuôn WP13-010: dựng lại bundle byte-exact, rồi mutation matrix reject
đúng typed code. Tối thiểu phải bắt được:
- transcript bị sửa một byte
- summary `status` bị đổi thành `pass` giả
- bundle của arm này bị gán nhãn arm khác
- config hash không khớp freeze

### RB-WP14-012 — Frontier report

`wp14_frontier_analyze.py` đã viết. Cần test + chạy + báo cáo.

Bảng bắt buộc trong báo cáo, mỗi arm một dòng:

| armId | factorLevel | completed | Δ vs B1 | median cell Δ | attributed burden | experienced movement | riders charged | worst-cell p95 rider drop |
|---|---|---|---|---|---|---|---|---|

Kèm scatter `(service, burden)`. **Không** cột "score", **không** sắp xếp theo tốt/xấu.

Phải đọc và trả lời thẳng ba câu:
1. F1 (freeze horizon thay whole-phase lock) gỡ được bao nhiêu trong 25/143 và
   41/212 block do pickup lock?
2. F2 (ratchet) có xảy ra lần nào không? Nếu cải thiện pickup không bao giờ xảy ra
   thì F2 **vô hiệu** và phải báo là vô hiệu, giống bốn factor ở `tasks/43` §3.
3. Budget sweep có tạo được điểm frontier ở giữa không, hay xác nhận lưỡng cực?

### RB-WP14-013 / 014 — Audit và closure

Theo khuôn WP13-012/013: audit từng file, DAG, claim boundary; rồi closure decision
mở hoặc không mở WP15.

---

## 9. Cần tiếp tục nghiên cứu và tối ưu cái gì

### 9.1 Tối ưu code — đã xác định, chưa làm

| Cơ hội | Bằng chứng | Ghi chú |
|---|---|---|
| Xoá 8/13 lexicographic level hằng số khỏi C1 profile | 0,000% phân biệt trên toàn panel | Đổi ngữ nghĩa objective ⇒ phải là ADR + factor, **không** phải refactor lặng lẽ |
| Bật `skipConstantObjectiveLevels` cho mọi run tương lai | 94–98% pass thừa | Đã có, mặc định tắt để giữ hash cũ |
| `candidate-id-rank` một level mỗi vehicle | 96,38% trong số đó hằng số | Có thể gộp nhưng phải chứng minh bảo toàn tie-break |

### 9.2 Nghiên cứu tiếp — literature đã đọc chỉ đường

Đã đọc full-text 77/77 trang, corpus tại
`E:\RideBoundData\research\pdf-20260824-wp14` kèm `fulltext-inventory.json`.

Hướng có bảo chứng, chưa làm:

1. **GenConVRP: chuyển hard consistency thành penalty + ε-constraint.** Lespay et al.
   tổng kết đúng chẩn đoán của chúng ta ("consistency requirements of ConVRP may be
   too restrictive") và đúng hướng xử lý. Đây là F3, đang hoãn.
2. **Outcome-anchored constraint thay promise-revision constraint.** Alonso-Mora ràng
   buộc `t^d_r ≤ t^*_r + Δ` — chất lượng **kết quả** so với chuyến đi lý tưởng.
   RideBound ràng buộc tổng biến phân của **lời hứa**. Hai object khác hẳn. Một
   variant ràng buộc outcome sẽ trả lời "rider quan tâm cái nào hơn".
3. **Compensating hold.** ConVRP variant nới thời điểm khởi hành "leads to improved
   time consistency, while the travel times remain almost unchanged". Tương đương:
   giữ xe chờ để bảo toàn lời hứa thay vì sửa lời hứa. Đây là F5, đang hoãn.

**Không** lấy tham số số học nào từ paper. `Ω = 2 min`, `Δ = 2Ω`, batch 30 s, LOS
`(5,10)/(7,15)/(10,20)` chỉ là context.

### 9.3 Đã loại bằng phép đo — đừng đề xuất lại

| Factor | Vì sao vô hiệu |
|---|---|
| Chỉ tính phần ETA xấu đi (one-sided charging) | 403/403 request có tiêu thụ đều là suy giảm ròng; 0 cải thiện |
| Neo net displacement thay total variation | `Σ|net|` = 99,9% tổng biến phân; trung bình 1,20 revision/request |
| Nới `vehicle_switch_count`, `*_stop_switch_count` | 0 witness trong 44.156 decision |
| Nới 7 dimension commitment còn lại | 0 witness trong 44.156 decision |

---

## 10. Giới hạn claim phải giữ trong bài báo

| Claim | Được phép | Không được phép |
|---|---|---|
| Service | "Trên panel H6 đã đóng băng này, C1 fail non-inferiority ở cả 8 và 4 xe" | Suy rộng population, external validity, SLA |
| Burden | "Operator không bao giờ chủ động đổi ETA đã publish"; giảm attributed 99,83% | "Rider có ETA ổn định"; 100% rider vẫn thấy promise đổi |
| Ranking | "C1 = B1 áp lên tập option đã bị gate cắt" | "C1 xếp hạng theo mức tiêu thụ commitment" — level đó là hằng số |
| Attribution WP9 `lock/ranking −3,7037 pp` | "chủ yếu là lock" | "là ranking" |
| Tỷ lệ hoàn thành tuyệt đối | So nội bộ giữa hai arm | So với literature — không arm nào có rebalancing |
| Cái giá của commitment | "cận dưới so với baseline reassignment đầy đủ" | "cận trên" |
| WP14 frontier | "development panel, exploratory" | Confirmatory; so trực tiếp `−154`/`−106` |
| Prune 83%/17% | "first-witness attribution, phụ thuộc thứ tự" | "phân rã layer" |

---

## 11. Checklist trước khi đóng bất kỳ ticket nào

- [ ] `dotnet test RideBound.slnx` — zero fail, zero skip
- [ ] pinned Python suite **từ thư mục `tests/`** — zero fail, zero skip
- [ ] `dotnet format --verify-no-changes`
- [ ] `git diff --check`
- [ ] Markdown link/fence, JSON parse, Python line ≤ 88 cho file mới
- [ ] Cập nhật `docs/18` (ADR + change history + tracker + bảng trạng thái),
      `docs/00-index`, `docs/16`, `docs/19`, `docs/tasks/23`, `docs/tasks/44`
- [ ] Nếu sửa file nằm trong 62 file freeze E1 ⇒ cập nhật
      `benchmarks/scenarios/wp13-e1/source-divergence-v1.json`
- [ ] Receipt mới có claim boundary rõ ràng
- [ ] Chỉ **một** ticket kế tiếp chuyển Ready

Nếu test fail khi chạy song song .NET + Python: chạy lại **tuần tự** trên máy rảnh
trước khi kết luận. Repo đã ghi nhận contention tạo timeout giả hai lần. Nhưng
**không** mặc định coi là flake — phải chạy lại sạch và ghi rõ.

---

## 12. Điều tuyệt đối không được làm

1. Không sửa H6: margin, panel, denominator, failed-job treatment, receipt.
2. Không dùng số đo H6/E1 để chọn mức factor.
3. Không nới nghĩa cờ/profile đã có; luôn tạo cái mới, mặc định tắt.
4. Không re-freeze E1 để "cho gọn".
5. Không bỏ `--include-behavioral-hash --require-audited-solver-evidence` khi verify.
6. Không nới ceiling CPU/resource để làm test xanh.
7. Không claim wall-clock speedup nếu chưa đo trong điều kiện kiểm soát.
8. Không xoá research corpora, vendor checkout, result artifact như "dọn dẹp".
9. Không commit trừ khi chủ repository yêu cầu.
10. **Không ép frontier phải đẹp.** Kết quả âm tính là kết quả hợp lệ và đã là đóng
    góp chính của nghiên cứu này.
