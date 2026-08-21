# WP8 closure evidence — RB-WP8-014

WP8 đóng ở trạng thái preregistered/materialized, **không** có confirmatory
effectiveness result.

Các blocker bàn giao đã đóng:

- burden oracle thực sự chạy ngoài tiến trình và mutation được bắt;
- pairing chặn cùng arm, đổi chỗ, khác arrivals và seed không còn nhân N;
- verifier chấp nhận contended lifecycle, recompute manifest/checkpoint/semantic
  hash và có audited solver gate;
- solver execution evidence serialize generation/prune/selection/fallback/bounds;
- rejection reason chỉ cụ thể khi mọi witness đồng nhất;
- Windows process tree nhận diện process instance, không cộng CPU do PID reuse;
- frontier 25/25 pass, baseline equivalence dùng behavioral hash;
- preregistration/freeze/materialization v2 đủ 20 cell, no outcome leakage.

Verification tại closure source state: `dotnet test RideBound.slnx` 840/840;
pinned adapter/analyzer/matrix/freeze tests 77/77. Candidate work-profile exact counters
giữ nguyên; hot-path process time giảm khoảng 20–23% trong ba lần đo nhưng đây
không phải SLA claim.

Giới hạn khoa học được ghi rõ: 20 fixed cells/5 travel days không cấp population
NI inference; 1,0 pp là exact panel gate; pickup locked share phải tách khỏi earned
drop share; C2/solver seeds chỉ exploratory robustness.

WP9 được mở bằng `RB-WP9-001`, không thay đổi outcome, margin, selection key,
primary treatment hay analysis sau freeze.

Sau closure draft nhưng vẫn trước outcome, adversarial review tìm thấy analysis chưa
bind exact job và HEAD/status chưa bind nội dung dirty tree. Amendment `WP8-011b`
và freeze receipt v2 `H3=d028eae4…dd14e` sửa integrity/source binding, giữ nguyên
toàn bộ thiết kế thực nghiệm. `RB-WP9-001` đóng bằng verifier receipt v2 PASS.

Sau đó, vẫn trước outcome confirmatory, kiểm receipt Layer-1 mechanical 8/8 phát
hiện H3 pin Runner cũ (`16f3b5e8…3aad`) thay vì build Release đã review và đã chạy
Layer-1 (`4c297a2c…bd2a8`). Amendment `WP8-011c` và receipt v3
`H4=2f7e6bf3…a32dd` repin artifact, bind cả 19-file publish tree; verifier recompute
25 file/Runner hash cùng derivative/scenario/Runner tree seals PASS. Không đổi thiết
kế hay claim; H2/H3 vẫn byte-nguyên làm lịch sử.
