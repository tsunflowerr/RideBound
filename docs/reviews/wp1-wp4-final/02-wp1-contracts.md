# WP1 — contracts, canonicalization và replay

## Vấn đề WP1 giải quyết

Nếu hai adapter hiểu khác đơn vị, thứ tự JSON, version compatibility hoặc retry,
mọi so sánh thuật toán phía sau đều vô nghĩa. WP1 khóa byte-level input/output và
session transaction trước khi có optimizer.

## Contract layer

- `ProtocolPrimitives.cs` và `ProtocolIdentityPrimitives.cs` tạo typed version,
  message/run/scenario/epoch/event/time/hash IDs, giới hạn integer tương thích
  cross-language.
- `CanonicalUnits.cs` khóa mm, milliseconds, WGS84 E7 và micro-cost; adapter
  không được âm thầm đổi đơn vị.
- `ProtocolEnvelope.cs`/`ProtocolEnvelopeCodec.cs` kiểm exact envelope field set,
  message routing và context fields bắt buộc.
- `HelloMessages.cs` khóa capabilities trước run; unsupported major/capability
  được reject hoặc downgrade theo matrix, không đoán.
- `InitializeRunMessages.cs` bind schema/core/policy/travel/data identity vào
  immutable manifest.
- `EventBatchMessages.cs` và `OnlineEventModels.cs` dùng typed event union thay
  cho dynamic dictionary.
- `DecisionMessages.cs`, `OnlineDecisionActions.cs` và `CommitmentContracts.cs`
  kiểm exact action/certificate shape và cross-binding state/publication IDs.
- `CheckpointMessages.cs` bind inner content với manifest/state/previous-decision
  hash; tên file không phải integrity mechanism.

## Canonical JSON và hash

`CanonicalJson.cs` giữ integer-only subset: sort object key ordinal, giữ array
order, reject duplicate/null/fraction/exponent/`-0`/invalid surrogate/out-of-range.
Điều này làm một semantic message có đúng một canonical byte sequence.

`ProtocolHash.cs` frame domain/tag/value lengths trước SHA-256. Decision hash bind
previous decision, manifest, policy version, canonical input và decision
projection. Certificate nằm trong projection; sửa promise publication hoặc
witness làm hash đổi. Hash không được tạo bằng nối string mơ hồ.

## Runner state machine

`RunnerSession.cs` thực thi:

```text
New -> Negotiated -> Initialized -> AwaitingDecisionApplied -> Initialized
```

- `hello`/`initializeRun` sai thứ tự bị reject;
- event batch phải có next epoch và contiguous event sequence;
- cùng key + cùng canonical bytes trả cached response; cùng key + payload khác là
  conflict;
- decision response không advance committed state;
- ACK sai context bị reject, sai decision hash fail session;
- chỉ ACK đúng mới advance epoch/sequence/state/previous-decision hash;
- checkpoint chỉ khi không có pending decision;
- restore kiểm manifest/hash/inner canonical state trước khi thay coordinator.

## Vì sao đây là tối ưu/correctness infrastructure

Canonical replay không làm route ngắn hơn, nhưng loại noise do transport,
dictionary order, retry và crash. Nhờ đó exact-small oracle, solver differential
và paired experiment phía sau đo policy thay vì đo adapter nondeterminism.

## Evidence

Contracts hiện có 133 tests: canonical vectors, strict schema, version/capability,
golden fixtures, hash tamper và checkpoint. Runner có process/session/retry/ACK
tests và clean-child-process transcripts. WP4 không thay protocol/hash framing;
nó chỉ dùng solver status shell đã tồn tại và bind thêm config vào manifest hash.
