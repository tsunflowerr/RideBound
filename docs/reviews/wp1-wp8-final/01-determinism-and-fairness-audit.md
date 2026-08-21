# Determinism, architecture và fairness audit

## Determinism

Static scan không thấy `Random`, `Guid.NewGuid`, wall-clock time hoặc số thực trong
Domain/Application/Algorithms decision path. `DateTimeOffset.UtcNow` còn ở receipt
metadata của benchmark, không tham gia state/policy/hash semantic. OR-Tools được pin,
single-worker, seed/deterministic-time bind trong adapter; solver evidence ghi rõ
termination/bound/fallback thay vì suy optimality từ status chung.

Mọi output-bearing enumeration đã kiểm đều có stable source order hoặc explicit
ordinal sort. Ba vòng dictionary C# không sort còn lại chỉ kiểm tính hợp lệ/set
membership; chúng không serialize hay chọn candidate. Python mapping sort graph,
vehicle, accepted IDs và canonical JSON; các `.items()` còn lại chỉ kiểm/collect
trước một bước sort hoặc set comparison.

Không lặp lại phát biểu sai “repo chỉ có hai catch”. Repo có nhiều typed catch tại
I/O/process/protocol boundaries; review kiểm chúng giữ first terminal failure hoặc
chuyển thành typed fail-closed code. Không thấy catch rỗng trong decision path.

## Architecture

Architecture tests và đọc project references xác nhận:

- Domain không package/reference bên ngoài;
- Application chỉ phụ thuộc Domain;
- Contracts/Benchmarking.Contracts không biết EF/ASP.NET/FleetPy/OR-Tools;
- OR-Tools chỉ nằm trong solver adapter pin `9.15.6755`;
- FleetPy Python chỉ gọi NDJSON Runner, không cài lại RideBound decision;
- BeGo/OptiGo không đi vào source tree hay core dependency.

## Fairness của B1/C1

Fairness không chỉ dựa vào hai JSON “trông giống nhau”. `EffectivePolicies` gỡ hard
limit/lock cho B1 và giữ chúng cho C1/C2; Runner validate bằng policy hiệu lực mà
decision tự công bố. Tests khóa:

- cùng raw candidate sets/work bounds;
- baseline không bị siết bởi file budget chung;
- treatment thực sự bị budget gate;
- no-op vẫn sống để defer thay vì làm run crash;
- falsification B1 tight/unbounded có cùng behavioral hash.

Frontier cho thấy cần tách hai can thiệp: C1 unbounded trả giá cho pickup lock; tight
budget là can thiệp thêm trên treatment. Không được gộp toàn bộ service loss thành
“giá của ngân sách”.

## Integrity của phân tích

`PairedComparisonDesign` bind orientation trong C#. WP9 analyzer còn bind exact row
trong execution plan, internal summary label, source scenario SHA-256 và full
Git-visible repository inventory. Primary gate dùng integer numerator/denominator:
`10_000 * deltaCompleted > -100 * arrived`; equality đúng biên 1 pp là fail theo
strict preregistration. Robustness analyzer có `confirmatoryGate: null`.
