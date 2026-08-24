# RB-WP13-001 — Post-H6 mechanism diagnostics refinement

> Trạng thái: `DONE`
> Ngày khóa: 2026-08-23
> Decision: ADR-053
> Ordered queue: [42-wp13-post-h6-mechanism-diagnostics-ticket-plan.md](42-wp13-post-h6-mechanism-diagnostics-ticket-plan.md)

## 1. Outcome

WP13 tạo một bản giải thích cơ chế có thể kiểm tra cho kết quả âm H6 mà không sửa
H6, không giả causal inference và không thiết kế policy v2 trước evidence.

Exit gate của WP13 là một record versioned cho từng cặp arm, phân loại được ranh giới
đầu tiên mà hành vi quan sát được khác nhau, immediate mechanism evidence có/không,
minimal relaxation khi witness hiện có đủ, và downstream outcome chỉ dưới nhãn
`trajectoryAssociated`.

## 2. Boundary bị đóng băng

- H6 Panel A/B, denominator, margin, panels, travel realizations, seeds, Runner,
  adapter, manifests, hashes và terminal outcomes là immutable historical evidence.
- `inputStateHash`, `stateBeforeHash` và `stateAfterHash` có policy/manifest identity;
  không được dùng một mình để căn chỉnh arm hoặc báo first divergence.
- Analyzer không tái hiện solver, candidate generator, commitment assessor hoặc
  simulator trong Python.
- Một rerun instrumented tương lai là exploratory experiment mới, có version/freeze
  riêng; không lấp field ngược vào H6.

## 3. Hợp đồng căn chỉnh

Đơn vị phân tích là cặp B1/C1 cùng frozen experimental unit. Analyzer đi lockstep từ
đầu transcript và dùng projection không chứa run/scenario/state/decision hash:

1. `observedInputProjection`: `epochId`, `simTimeMs`, ordered event type/payload;
2. `wireDecisionProjection`: solver status và ordered actions sau khi bỏ duy nhất
   generated `candidateId`/`publicationId`;
3. `operationalDecisionProjection`: giữ nguyên order request/plan/breach actions,
   nhưng canonical-sort riêng các slot `promisePublished`, vì Runner sắp publication
   theo generated `publicationId`; cùng một tập promise có thể khác wire order giữa arm;
4. nếu input projection khác trước operational decision khác, record là
   `observedInputDivergence`, không phải same-state comparison;
5. nếu input projection bằng nhau và operational projection khác, record là
   `operationalDecisionDivergenceOnEqualObservedInput`;
6. wire-only publication reorder được lưu riêng nhưng không phải mechanism divergence;
7. nếu một transcript kết thúc/lệch protocol, fail closed.

Tên “equal observed input” là cố ý: nó không khẳng định toàn bộ internal state bằng
nhau. Same-state attribution mạnh hơn chỉ được ghi khi evidence C# versioned chứng
minh các projection cần thiết.

## 4. Evidence sufficiency đã biết

| Câu hỏi | H6 v1 | Hành động |
|---|---|---|
| Bundle/provenance/exogenous identity | Đủ | tái dùng independent WP9 verifier |
| First observed divergence | Đủ | projection contract mới |
| B1 candidate bị C1 prune bởi witness nào | Đủ khi candidate ID xuất hiện trong `prunedCandidates` | link exact witness |
| Full accepted candidate portfolio | Không đủ | không suy diễn; Runner evidence vNext nếu cần |
| Rerank objective mới | Không đủ | exploratory rerun sau freeze mới |
| Downstream causal effect | Không đủ theo thiết kế finite trajectory | chỉ `trajectoryAssociated` |

## 5. Metrics và claim boundary

- Primary diagnostics: loại divergence, epoch/time, request/action projection, exact
  prune witness, minimum observed lock/budget relaxation nếu phép tính xác định.
- Descriptive distributions: tỷ lệ pair/cell theo mechanism, immediate service delta,
  thời gian tới divergence, downstream completed delta.
- Không CI/population inference; không rescue primary H6; không nói satisfaction,
  novelty, SLA hoặc general external validity.

## 6. DoR/DoD cho implementation đầu tiên

`RB-WP13-002` chỉ inventory evidence và khóa projection contract. Nó phải:

- fail closed khi bundle pairing, transcript framing hoặc required fields sai;
- chứng minh state hashes khác không tạo false divergence;
- phát hiện decision/input/end-of-transcript divergence bằng mutation tests;
- phát report canonical JSON, không thay raw evidence;
- chạy full FleetPy suite và `dotnet test RideBound.slnx`.
