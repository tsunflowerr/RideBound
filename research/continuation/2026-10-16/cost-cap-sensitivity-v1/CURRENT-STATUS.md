# C60/C120 — đủ lượt chạy, còn kiểm cuối và phân tích

Checkpoint **2026-10-16T17:17:22.473151+07:00**: **2/2 smoke và68/68 main pass/succeeded;0active/0pending;driver đang full post-phase check**.
Driver11224/launcher14260 còn sống;không tạo/resume driver. Rootlog đã ghi
main68/68 pass rồi full original/new frozen-input check begins.
**Chưa có main-complete.json,analysis-started/complete hoặc final exit0** tại
checkpoint này;chưa có kết quả hiệu quả mới để diễn giải hoặc sửa luận văn.

Đã kiểm cơ học đủ70bundle(68main+2smoke):mọi length/SHA/nativeexit0 và
admission/finish binding khớp;96pinned+17seal+5policy khớp.0lỗi/STOP/drift/
unexpected output. Evidence:`heartbeat-20261016-171722-mechanical.json`,SHA256`98d5877b400db79f619e1a2047b1f8ac55b22e9ee111c4f7f0f37650e9437322`.
Manifest:`cedecbb1a97d31ec8ab295fd16c99fb607f14d622779247d8df6d9b219b42d6f`.
AC100%,C35.816GiB/E40.765GiB;C30/E40/AC/410MiB/6workers giữ nguyên.

## Khi driver kết thúc

Driver tự phân tích sau main post-check đạt;không chạy analysis trùng. Chỉ khi
rootlog có exit0,driver/launcher/analysis children đã hết,analysis-complete.json
vàresults/REPORT.md tồn tại,mọi stop/mismatch đều không có,mới kiểm báo cáo.
Sau đó chạy checker số học độc lập mới(READ-ONLY trên kết quả):

C:/Users/quang/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe
-X utf8 -I -B C:/RideBoundContinuation/2026-10-16/verify-cost-cap-final.py

Checker SHA256:`0cf5a4d966d82afaf44c1b4ac7131944eae7907e12bd9b1c5b01d5e420cfbd1b`. Đây là công cụ kiểm mới ngoài harness
đã khóa;không đổi driver/analysis/manifest hoặc nghiên cứu. Nó chỉ đối chiếu
CSV/JSON/cell/day/baseline/receipt,không nhận raw-ledger/forecast decoding độc
lập. Output độc quyền C:/RideBoundContinuation/2026-10-16/cost-cap-final-independent-arithmetic.json.
Nếu output đã có thì đọc/hash/đối chiếu trước,không overwrite/chạy lại bừa.
Nếu checker trượt,giữ kết quả và lỗi;không sửa analysis/raw để cứu số.
.NET kiểm lại1033pass/1034,1known clean-tree failure,exit1;log+summary ở
C:/RideBoundContinuation/2026-10-16/dotnet-test-final-verification-20261016.log
vàdotnet-final-verification-summary.json,không gọi suite xanh.

Khi kiểm kết quả đạt:cập nhật docs18/19/ADR079/RESUME/CURRENT,báo cả đánh đổi
và giới hạn,push báo cáo/hash nhẹ vào nhánh bàn giao. Giữ PDF77trang và nội
dung luận văn hiện có cho đến khi báo kết quả xong;không tự sửa từ partial.
Sau khi báo cáo đầy đủ và push xong,tắt hourly,giữ one-off PAUSED,không archive.

## GitHub đã push

- [Mã, kết quả và audit](https://github.com/tsunflowerr/RideBound/tree/codex/research-handoff-20261016),
  commit đã push trước checkpoint: `fafee2f388b36df535fda565a48d2c9244fbb0e1`.
- [Khóa luận và chương trình/báo cáo nghiên cứu](https://github.com/tsunflowerr/Report_INT3508/tree/codex/ridebound-handoff-20261016),
  commit `1d529f5f7629b7e13c9bb608737cef5b668cf313`. PDF77trang giữ hash26c8dea1…1dd69.
- Nguồn runner riêng `research/tier3-cost-first` đã push dce84ec.
- Đọc START-HERE.md ở hai repo. GitHub giữ nguồn/báo cáo/hash; raw lớn,
  dữ liệu/runtime/vendor và toàn văn paper ở máy gốc. Không có xóa dữ liệu.

## Tiếp tục an toàn

Driver tự làm2smoke→68main→analysis. Đủ68 và final checks mới có báo cáo.
Không tạo driver trùng; không retry failed/partial/anchor-only hoặc analysis
started dở dang. STOP/*-FAILED.txt/INPUT-DRIFT-STOP.txt của đợt mới/cũ giữ nguyên.
Chỉ resume pending sau resource deferral khi driver và children đã hết, không
partial/anchor-only, AC và C30/E40GiB đủ,410MiB/job, tối đa6workers.

Hidden Windows PowerShell -NoProfile -ExecutionPolicy Bypass -File
C:/RideBoundContinuation/2026-10-16/cost-cap-sensitivity-v1/launch.ps1
-Mode run -Log <log C mới,exclusive> -ManifestSha256 <hash trên>.
Không dùng launcher Stage1b/resource30 cũ. Không sửa code/plan/manifest đã khóa.

Hourly automation theo-d-i-ridebound-stage-1b-theo-gi ACTIVE cho đợt mới;
one-off06:20 PAUSED. Sau báo cáo đầy đủ và push kết quả thì tắt hourly, không
archive. Owner đã cho commit/push báo cáo nhẹ liên quan, không force-push hoặc
merge main. Không sửa luận văn từ partial results, sửa dự báo, mở per-step/Stage2
hoặc cứu H6/E1/H7. Audit mức sẵn sàng bảo vệ vẫn còn mở.
