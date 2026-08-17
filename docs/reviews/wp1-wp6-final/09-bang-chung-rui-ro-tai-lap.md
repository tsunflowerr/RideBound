# Bằng chứng, rủi ro và tái lập

## 1. Gates hiện hành

```powershell
dotnet format RideBound.slnx --verify-no-changes
dotnet build RideBound.slnx -c Release -warnaserror
dotnet test RideBound.slnx
dotnet list RideBound.slnx package --vulnerable --include-transitive
```

Required command tại closure: 770/770 pass, exit 0. Contracts và Runner DLL nạp được;
Windows Application Control `0x800711C7` không tái hiện. Các lần WAC cũ vẫn là evidence
lịch sử, không bị sửa thành pass.

WP5 external regression read-only: BeGo backend exit 0 với 149 pass + 5 explicit
integration opt-in skip (154 discovered), frontend 9/9. Đây không được gọi là fresh
154/154 PostgreSQL/published-Runner pass; full opt-in evidence lịch sử được giữ riêng.

## 2. Fresh tiny

```powershell
dotnet run --project tools/RideBound.Wp6TinyHarness -c Release -- `
  --repository E:\Code\RideBound `
  --work-root E:\RideBoundData\wp6-tiny-work-20260813-closure-a `
  --bundle E:\Code\RideBound\artifacts\wp6\tiny-paired-20260813-closure-a `
  --receipt E:\Code\RideBound\artifacts\wp6\tiny-paired-20260813-closure-a.receipt.json `
  --configuration Release
```

Kết quả: 8/8, bundle `79cb321a...b04`. Dùng path mới; tool cố ý từ chối overwrite.

## 3. Fresh public-medium pair

H/I dùng cùng `--cache E:\RideBoundData\wp6`, exact source cuối, hai work root/bundle/
receipt mới. Mỗi process chạy B1/C1 × (1 warm-up + 3 measured) và mất khoảng 506–509 giây.

Kết quả H/I:

- planned/succeeded/failed/excluded: `8/8/0/0` cả hai;
- plan `1b433b82...14d6`, scenario `88a8730a...0e88`;
- source inventory `08b4f78b...3d26`, runtime inventory `1121a9b3...1dfd`;
- 16 top-level semantic mismatch: 0;
- 8 run × 9 semantic field mismatch: 0;
- full resource row khác: 8/8, expected;
- bundle H `89a43921...d9d8`, I `a954db62...94e9`.

## 4. External verifier

```powershell
dotnet run --project tools/RideBound.Wp6BundleVerify -c Release -- `
  --bag <sealed-bundle-directory> --report <new-file-outside-bundle>
```

Verifier tự hash executing assembly, không sửa sealed bag và chỉ tạo sidecar mới. Tiny,
H và I đều exit 0 và in đúng physical bundle hash.

## 5. Những rủi ro còn mở

- Không có FleetPy closed-loop semantics: chờ WP7.
- O-006 edge-progress capability cần executable preflight.
- Budget/threshold/non-inferiority margin chưa preregister: chờ WP8.
- Resource measurement là local control; non-Windows process-tree coverage yếu hơn.
- Candidate caps có approximation loss được ghi diagnostic nhưng chưa có workload-wide
  effectiveness bound.
- Incident recovery/cross-vehicle reassignment chưa có optimizer.
- No user-satisfaction/fairness claim vì thiếu nhãn thật.

## 6. Cách đọc một failure

Không quy mọi failure thành “test đỏ”. Xác định boundary:

- protocol/schema/hash → WP1;
- state/lifecycle/physical → WP2;
- promise/lock/budget/publication → WP3;
- candidate/solver/fallback → WP4;
- lease/transaction/recovery/outbox → WP5;
- dataset/process/store/oracle/bundle/claim → WP6.

Sau đó đọc typed code/stage/witness và retained raw evidence. Không delete artifact lỗi;
nó có thể là bằng chứng quan trọng hơn một bundle pass.

## 7. Verdict claim

Được nói: “WP1–WP6 mechanical correctness/reproducibility gate đã pass trên evidence
đã ghi.” Không được nói: “RideBound tốt hơn B1”, “đạt production SLA”, “FleetPy đã xác
nhận effectiveness”, hoặc “người dùng hài lòng hơn”.
