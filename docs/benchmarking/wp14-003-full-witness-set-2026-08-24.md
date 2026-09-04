# RB-WP14-003 — Witness set đầy đủ trong evidence profile

> Ngày: 2026-08-24
> Trạng thái: `Done`
> Claim class: instrumentation completeness; không đổi feasibility hay kết quả nào

## 1. Vấn đề

`CommitmentDecisionValidator` duyệt request theo thứ tự ID và **return ngay** ở
request fail đầu tiên; bên trong một request, lock được kiểm trước budget và cũng
return ngay. Witness ghi được vì vậy là “request fail đầu tiên, layer fail đầu
tiên”, không phải tập đầy đủ.

Hệ quả cho phân tích: tỷ lệ 780 budget / 160 lock (Panel A) và 491/92 (Panel B) là
attribution **phụ thuộc thứ tự**, không phải phân rã. Một candidate vi phạm cả hai
layer chỉ được ghi một. Fail-fast là đúng cho hot path, nhưng nó làm evidence không
đủ để trả lời “nếu bỏ lock thì budget có chặn không”, đúng câu hỏi mà factor F1 của
WP14 cần.

## 2. Thay đổi

`CommitmentValidationContext.CollectAllCommitmentWitnesses`, mặc định `false`.

Khi bật, validator đánh giá **lock và budget cho mọi request** trước khi kết luận,
rồi trả về toàn bộ witness. Khi tắt, đường đi và chi phí không đổi một dòng.

Ranh giới cố ý giữ nguyên:

- lỗi **structural** (projection failure, ledger conflict, policy not found) vẫn
  return ngay kể cả khi bật, vì quét tiếp trên một state đã hỏng không có nghĩa;
- request đã fail **không** được append revision, nên ledger không bao giờ tiến;
- `CandidatePruneWitness.Code` vẫn lấy `Witnesses[0]`, tức witness đầu theo thứ tự
  quét, nên mã prune ở tầng trên không đổi ngữ nghĩa; chỉ **danh sách** đầy đủ hơn.

## 3. Một lỗi thiết kế đã bị bắt và sửa

Bản đầu tiên suy cờ từ profile đã có:
`CollectAllCommitmentWitnesses == (profile == "retained-portfolio-v1")`.

Điều đó **sai**, và mức độ nghiêm trọng vượt xa một test đỏ. Hai configuration đã
đóng băng của E1 —
[`wp13-e1-fleetpy-rolling-cost-retained-v1.json`](../../benchmarks/configurations/wp13-e1-fleetpy-rolling-cost-retained-v1.json)
và
[`wp13-e1-fleetpy-ridebound-hard-vector-retained-v1.json`](../../benchmarks/configurations/wp13-e1-fleetpy-ridebound-hard-vector-retained-v1.json)
— đều khai báo đúng profile đó. Nới profile tại chỗ nghĩa là chạy lại E1 bằng chính
config đã freeze sẽ cho witness list khác ⇒ evidence khác ⇒ `decisionHash` khác ⇒
freeze receipt của E1 và tính chất byte-exact rebuild của WP13-010 không còn tái
lập. Không test nào bắt được vì không có test nào replay E1.

Sửa bằng một profile **riêng**: `retained-portfolio-full-witness-v1`.
`retained-portfolio-v1` giữ nguyên ngữ nghĩa vĩnh viễn.

Thêm hai regression khoá tính chất này lại: một test khẳng định
`retained-portfolio-v1` **không** bật thu thập đầy đủ, và một test đọc thẳng hai
file config E1 thật rồi khẳng định điều đó cho từng file. Nếu ai đó nới profile cũ
trong tương lai, test thứ hai đỏ ngay.

Đây là cùng một bài học với ADR-067: evidence nằm trong hash, nên mọi thay đổi làm
đổi evidence đều phải là profile/cờ mới, không bao giờ được nới nghĩa của cái cũ.

## 4. Verification

| Kiểm tra | Kết quả |
|---|---|
| Mặc định tắt | pass |
| Verdict giống hệt fail-fast trên 5 mức hard limit (0, 5, 10, 1.000, unbounded) | pass |
| Candidate bị từ chối không advance ledger khi đang collect | pass |
| Witness budget vẫn được báo đúng dimension | pass |
| **Fail-fast che layer budget sau layer lock**; collect-all báo cả hai | pass |
| Profile mới bật cờ; `retained-portfolio-v1` **không** bật; profile lạ bị reject | pass |
| Hai config E1 đã freeze đọc từ đĩa: profile `retained-portfolio-v1`, cờ tắt | pass |
| Required `dotnet test RideBound.slnx` | 880/880, zero skip |
| Full pinned CPython/FleetPy | 225/225, zero skip |
| `dotnet format --verify-no-changes`, `git diff --check` | pass |

Test quan trọng nhất là cái thứ năm: nó dựng một policy vừa freeze drop-ETA trong
horizon vừa cho drop-ETA budget bằng 0, nên cùng một candidate phá cả hai layer.
Fail-fast chỉ báo lock; collect-all báo cả lock lẫn budget. Đó là bằng chứng trực
tiếp rằng attribution cũ thiếu, chứ không phải suy đoán.

## 5. Điều không claim

Không claim rằng tỷ lệ 780/160 và 491/92 của H6/E1 là sai. Chúng đúng như định
nghĩa “first witness”, và mọi kết luận đã công bố chỉ dùng chúng ở mức đó. Cái
được sửa là evidence **tương lai** đủ để phân rã layer; dữ liệu cũ không được ghi
đè và không được diễn giải lại.

Cũng không đo lại H6/E1 bằng cờ mới: làm vậy sẽ là một run mới trên panel đã đóng
băng, và ADR-065 cấm.

## 6. Hệ quả

`RB-WP14-003 Done`, `RB-WP14-004 Ready`. `004` phải dựng development panel mới mà
không chạm H6 — đây là ticket quyết định WP14 có chạy được matrix hay không.
