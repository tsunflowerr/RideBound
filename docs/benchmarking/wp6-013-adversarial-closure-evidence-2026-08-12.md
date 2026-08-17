# RB-WP6-013 — bằng chứng đóng adversarial determinism/failure/resource

> Ngày: 2026-08-12  
> Evidence class: `mechanical`  
> Claim profile: `wp6-mechanical-only-v1`  
> Contract: `1.0.6`, ADR-035  
> Kết luận: required-mutant matrix của ticket đã bị chặn đầy đủ; đây không phải
> general mutation score, FleetPy closed-loop experiment, effectiveness result hay SLA.

## 1. Ticket này chứng minh điều gì?

`RB-WP6-013` không thêm một KPI đẹp rồi gọi là tối ưu. Ticket kiểm tra xem toàn bộ
đường đo WP6 có còn đúng khi thứ tự property/input thay đổi, nhiều thread/process
cùng chạy, child process lỗi ở từng nhánh, raw evidence bị sửa hoặc resource sample
khác nhau giữa hai lần chạy hay không.

Chuỗi được kiểm là:

```text
canonical public scenario + pre-outcome plan
→ fresh exact Runner process cho từng run
→ immutable raw/terminal evidence
→ production metric + independent process oracle
→ strict semantic bundle + claim checker
```

Hai miền được tách rõ:

- **semantic identity** phải giống byte/hash giữa fresh processes;
- **sampled resource identity** được phép khác, nhưng mọi sample phải được giữ,
  kiểm invariant và bind vào bundle. Không được bỏ strata chậm hay đổi nó thành 0.

## 2. Những lỗ logic audit đã tìm thấy và đã sửa

| Lỗ thực | Tại sao test xanh vẫn có thể sai | Correction đã khóa |
|---|---|---|
| Generic paired harness đặt `warmupRunCount=0` | Ticket yêu cầu warm-up nhưng evidence cũ chỉ có ba measured repeat | plan hiện có 1 warm-up + 3 measured mỗi arm, tổng 8 run; warm-up có run ID riêng và không donate state/cache |
| Thông báo preflight nói claims/pins/sources đã sorted nhưng code chỉ sort claims | provenance có thể fail rất muộn sau các Runner run đắt | claims, absolute pins và bundle sources đều non-empty, ordinal-sorted, case-insensitive unique; source path/media type được validate trước execution |
| Public policy mismatch chỉ lộ sau khi Runner chạy | scenario synthetic policy có thể không khớp config nhưng tốn cả grid mới fail | policy ID được derive từ request set và exact single config binding phải khớp trước run đầu tiên |
| Conversation có thể trả arbitrary failure code | store có thể nhận taxonomy/stage không thuộc contract | chỉ 5 conversation codes giữ canonical stage; code ngoài catalog trở thành `protocol.invalid-output/parsing` với safe witness |
| Run conservation hard-code đúng 6 terminal | thêm warm-up làm harness tự mâu thuẫn với plan | conservation lấy `compiled.PlannedRuns.Count`, không dùng literal |
| Process test đọc nhầm `data/plan/benchmark-plan.json` | test không kiểm được plan thật | locator sửa thành exact bundle path `data/benchmark-plan.json` |

Correction này không nới WP3 validator, không thay public data, không đổi WP4
objective và không chọn O-002/O-003/O-004.

## 3. Required-mutant matrix

Tỷ lệ `100% killed` dưới đây chỉ áp dụng cho **tập mutation bắt buộc được liệt kê
trong ticket/contract**, không phải phần trăm mutation chung của mọi dòng C#.

| Boundary | Required mutant/property | Bằng chứng executable |
|---|---|---|
| 10 WP6 documents | đảo property đệ quy trong mọi nested object; 16 parallel decode mỗi document | `BenchmarkContractCodecTests.Nested_property_permutation_and_parallel_decode_are_exact_for_every_document` |
| Failure taxonomy | 21 canonical `(code,stage)` case, gồm `artifact.mismatch` preflight và postflight; wrong stage bị từ chối | `FailureCodesAndStages` + codec semantic validator |
| Exclusion taxonomy | đủ 8 pre-outcome rules; mọi `beforeOutcome=false` bị từ chối | `ExclusionRuleIds` + codec/store tests |
| Plan/seed/order | permutation input và 32-way parallel compile phải có exact plan/run sequence; HMAC order phải tạo cả B1-first và C1-first strata | `BenchmarkPlanCompilerTests`, `TinyPairedHarnessProcessTests` |
| Provenance preflight | claims, pins hoặc sources reverse/duplicate/case-collision/path collision | `MechanicalPairedHarnessPreflightTests` |
| Public policy binding | scenario policy khác/multiple/empty config policy | `PublicDerivativePairedHarness` preflight regressions; historical wrong `uniform-v1` remains rejected |
| Actual child process | start failure, crash, cancel, wall/CPU/memory/process-count, stdin/stdout/stderr, postflight drift, incomplete output, unsupported conversation code | `ExternalProcessSupervisorTests`; partial raw evidence/process-tree cleanup retained |
| Terminal mapping | đủ 21 failure/stage cases map tới deterministic raw evidence role | `ExternalProcessTerminalMapperTests.FailureEvidenceMatrix` |
| Immutable store | seven injected write boundaries, seal-vs-commit race, cross-run concurrency, log gap/reorder/tamper, outcome exclusion, private safe text, unknown failure | `AppendOnlyRunStoreTests` |
| Metric | request/action/promise/vector/window/order/resource/denominator-zero/overflow mutations; production/oracle mismatch | `MechanicalMetricCalculatorTests`, external `RideBound.Wp6MetricOracle`, strict verifier recomputation |
| Bundle stages | missing/extra/tamper/length/media/path/traversal/case/reparse/script/scenario/provenance/grid/transcript/log/oracle-only/correlated metric | `StrictBagItBundleTests`; every declared class fails at its expected ordered stage |
| Claim surface | forbidden wording/synonym/case/punctuation/confusable/default-ignorable, missing caveat, forged report/profile/provenance | stage-10 claim checker tests; both D/E reports are `passed` |
| Hidden nondeterminism | mutable RNG, runtime hashing and raw wall-clock use in semantic sources | `Wp6SourceAuditTests` exact allowlist |

Source audit allowlist hiện chỉ còn:

```text
SafeZipExtractor.cs                         RandomNumberGenerator ×1
VerifiedDatasetDownloader.cs               RandomNumberGenerator ×1
TinyPairedHarness.cs                       DateTimeOffset.UtcNow ×2
RideBound.Wp6Normalize/Program.cs           Guid.NewGuid ×1
```

Hai cryptographic RNG occurrence chỉ tạo tên staging chống collision; `Guid` chỉ đặt
clean temporary root; UTC chỉ là provenance start/finish. Không giá trị nào tham gia
scenario/plan/run/transcript/decision/semantic-metric identity. Call-site audit còn
đúng một producer metric trong paired harness và một recomputation trong strict
verifier; independent oracle không reference production calculator/model.

## 4. Fresh-process medium reproduction D/E

Hai lệnh dùng cùng verified cache/source nhưng work root, bundle và receipt riêng:

```powershell
dotnet run --project tools/RideBound.Wp6MediumHarness -c Release -- `
  --repository E:\Code\RideBound --cache E:\RideBoundData\wp6 `
  --work-root E:\RideBoundData\wp6-medium-work-20260812-d `
  --bundle E:\Code\RideBound\artifacts\wp6\public-medium-20260812-release-d `
  --receipt E:\Code\RideBound\artifacts\wp6\public-medium-20260812-release-d.receipt.json `
  --configuration Release

dotnet run --project tools/RideBound.Wp6MediumHarness -c Release -- `
  --repository E:\Code\RideBound --cache E:\RideBoundData\wp6 `
  --work-root E:\RideBoundData\wp6-medium-work-20260812-e `
  --bundle E:\Code\RideBound\artifacts\wp6\public-medium-20260812-release-e `
  --receipt E:\Code\RideBound\artifacts\wp6\public-medium-20260812-release-e.receipt.json `
  --configuration Release
```

Mỗi process materialize 1 warm-up + 3 measured repeat cho B1 và C1:

| Identity/count | Release D | Release E | Gate |
|---|---|---|---|
| planned/succeeded/failed/excluded | `8/8/0/0` | `8/8/0/0` | exact |
| plan | `d2396ce94602eda2ab0b55441fc43515e76f86b7735efe0317cd7255a5f3912a` | same | exact |
| scenario | `88a8730afb6149052fbe97672e5cf77f9bd352b47a7039735b7e985140370e88` | same | exact |
| source inventory | `12b03b2fb8a39214f0a25b91509ea25a824246783127c58891d5290db01669ae` | same | exact |
| runtime inventory | `34e16bcb0524a8e864a2478b4ac21ce3836ce88519cfc27d9c90d07d7e4554a9` | same | exact |
| run grid | `93965943b4a4e44e6834c84889259ca18adb6efd01357023cf9cc6e6cef76341` | same | exact |
| transcript set | `e45a7f940f9818c5c6a9fa977784e326ea6e28e515d5d9a70f8f62107b640300` | same | exact |
| decision set | `57ef86f3d59e411c3821affc20223e63a35c826c4d96654e2829b0e370d6f102` | same | exact |
| semantic metric set | `703404e18f22c157956295cd8a91f0e5160b41b4774abc144260932731ee6c08` | same | exact |
| full metric set | `d13db93f0e5ad6bcf8821c40bf546ef2bdcced7e5dedf3eaec9b01d70351941d` | `fcb58cbd26b63e51ea6eb267d5defce51cc2de541fc7d0f7acbb874b217f9ce3` | expected different |
| logical manifest | `67ef7ca9965ea041be5337f21905e1f474c0f3820809d3384b9f4036d77ab55a` | `ff0bf135f4e9ef54fb3c0ce5502f2d2dcafb0dbe8e3a2533484d46b0c3c3b2b5` | expected different |
| physical bundle | `cb6597d89a844099d5af4849f895d0c5f7af3d7351be5345cfcbf558180324a0` | `27c7f69e5df77b4f1136c75252e8abb9371231e76e5df55c2a5b42c427d2514e` | expected different |
| external verify | valid, report `ec45586d...ce35` | valid, report `3659271a...38c2` | valid |

Máy so sánh đọc receipt hiện hành và xác nhận 13/13 top-level semantic fields khớp,
0 semantic mismatch trên 8 run và 8/8 full metric-row hash khác nhau. Hai verifier
report đều `isValid=true` và bind cùng verifier assembly
`04857e69124e9c6e85f0d13b2df7dc9aade7237818ce65f6316964a68021c067`.
Hai claim report đều `passed`, `sameTeamCleanProcessOnly=true` và
`resourceMeasurementsLocalControlsOnly=true`.

## 5. Raw resource strata — giữ cả kết quả âm

Đơn vị: wall/CPU là ms, peak là byte. `repeat=0` là warm-up; `1..3` là measured.

| Process | Arm | Repeat | Warm-up | Wall | CPU | Peak working set | Process max |
|---|---|---:|---|---:|---:|---:|---:|
| D | B1 | 0 | yes | 58,376 | 57,624 | 175,681,536 | 2 |
| D | C1 | 0 | yes | 65,961 | 64,218 | 191,102,976 | 2 |
| D | B1 | 1 | no | 59,467 | 58,265 | 199,028,736 | 2 |
| D | C1 | 1 | no | 64,470 | 62,686 | 178,319,360 | 2 |
| D | B1 | 2 | no | 58,497 | 57,311 | 177,860,608 | 2 |
| D | C1 | 2 | no | 65,181 | 63,686 | 198,332,416 | 2 |
| D | B1 | 3 | no | 56,431 | 55,233 | 167,485,440 | 2 |
| D | C1 | 3 | no | 65,502 | 64,453 | 168,701,952 | 2 |
| E | B1 | 0 | yes | 57,782 | 55,905 | 172,916,736 | 2 |
| E | C1 | 0 | yes | 64,540 | 63,218 | 167,608,320 | 2 |
| E | B1 | 1 | no | 57,803 | 56,030 | 166,965,248 | 2 |
| E | C1 | 1 | no | 66,383 | 64,952 | 175,476,736 | 2 |
| E | B1 | 2 | no | 57,272 | 55,874 | 169,005,056 | 2 |
| E | C1 | 2 | no | 64,698 | 62,984 | 177,111,040 | 2 |
| E | B1 | 3 | no | 59,061 | 58,608 | 194,400,256 | 2 |
| E | C1 | 3 | no | 64,315 | 63,343 | 169,496,576 | 2 |

C1 có wall và CPU lớn hơn B1 trong cả 6 measured pair cục bộ. Kết quả âm này được
giữ nguyên. Không thể suy ra C1 “chậm hơn trong sản xuất”, cũng không thể đánh đổi nó
với service/commitment benefit vì instant-drain hoàn tất mọi request cùng timestamp,
không có vehicle motion hay KPI vật lý. Statistical estimand, non-inferiority và
FleetPy closed-loop vẫn thuộc WP7–WP9.

## 6. Quality gates từ worktree hiện hành

```powershell
dotnet build RideBound.slnx --configuration Release --no-restore -warnaserror
dotnet format whitespace RideBound.slnx --no-restore --verify-no-changes
dotnet list RideBound.slnx package --vulnerable --include-transitive
dotnet test tests\RideBound.Benchmarking.Contracts.Tests\RideBound.Benchmarking.Contracts.Tests.csproj `
  --configuration Release --no-build --filter "FullyQualifiedName~SchemaContractTests"
dotnet test RideBound.slnx
```

Kết quả cuối:

- Release build: 0 warning, 0 error;
- format: pass;
- vulnerability audit: không project nào có direct/transitive package vulnerability;
- schema gate: 4/4 pass;
- Markdown/link gate read-only: 91 file, 180 internal file link, 0 broken, 0 code
  fence lệch;
- required full solution: **770/770**, 0 fail, 0 skip — Architecture 10,
  Contracts 135, Domain 135, Application 69, Algorithms 136, Solver 7,
  Benchmarking.Contracts 71, Runner 72, Benchmarking 135.

Một required run ngay trước baseline cuối đạt 769/770: medium exact-Runner test vượt
CPU control 120 giây dưới full-suite contention. Cùng test chạy riêng pass 1/1 trong
1 phút 33 giây; lệnh exact rerun sau đó pass 770/770 trong khoảng 2 phút 13 giây.
Đây là retained local resource variance, không phải full-pass giả và không phải WAC.
`RideBound.Contracts.dll`/`RideBound.Runner.dll` đều build/load/run; mã
`0x800711C7` không tái xuất hiện.

## 7. Verdict và handoff

`RB-WP6-013` được chuyển `DONE` vì tất cả acceptance của ticket có artifact/lệnh
kiểm trực tiếp: required mutation matrix bị kill, plan conservation exact, tiny và
medium fresh-process semantic reproduction đạt, raw resource strata không bị lọc,
và toàn bộ quality gates cuối pass.

Điều chưa được chứng minh vẫn được ghi rõ:

- không có FleetPy closed-loop vehicle motion;
- không có effectiveness/non-inferiority/SLA/production claim;
- không có independent-team reproduction;
- chưa có pilot/preregistration và chưa chọn O-002/O-003/O-004.

Ticket kế tiếp duy nhất là `RB-WP6-014`: audit source/claim toàn WP1–WP6, đọc lại
toàn bộ Markdown, kiểm sâu logic WP3/WP4/WP6, rerun E2E/verifier và viết folder review
tiếng Việt trước khi cân nhắc đóng WP6.
