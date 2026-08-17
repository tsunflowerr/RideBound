# WP1–WP2: contract, state và physical baseline

## WP1 — byte nào là sự thật?

`CanonicalJson` parse JSON nghiêm, cấm comment/trailing comma/null/duplicate property,
cấm số không nguyên, `-0`, exponent và số ngoài safe integer. Object key được sort
ordinal; string được kiểm Unicode scalar và escape ổn định. Vì vậy cùng document có
một biểu diễn byte duy nhất.

`ProtocolHash` không nối chuỗi mơ hồ. Mỗi loại hash có domain riêng; mỗi field có tag,
độ dài tag và độ dài value theo big-endian. Decision hash bind previous hash, manifest,
policy version, canonical input state và canonical decision. Đổi thứ tự/frame/value
đều đổi hash.

`ProtocolEnvelopeCodec` kiểm exact envelope fields và context theo message type:
hello/shutdown không mang run context; initialize/checkpoint mang run context; event/
decision/ACK mang cả epoch/time. Payload codec mới chịu trách nhiệm semantic fields.

`RunnerSession` là state machine, không phải endpoint stateless. Nó khóa thứ tự
`hello → initialize → eventBatch → decision → decisionApplied`; exact retry trả cached
response, còn cùng epoch nhưng khác canonical request là conflict.

## WP2 — state nào được phép đổi?

`RideBoundRun` là immutable aggregate cho request/vehicle. Mỗi method tạo bản sao mới
sau khi lifecycle guard pass. Accepted request không thể bị reject như request pending;
board/alight cập nhật request và vehicle cùng operation.

`EventReducer` kiểm batch identity, epoch tăng đúng một, time không lùi, sequence liên
tục và mọi event có đúng batch time. Nó áp event lên local `run/travel/incidents`; chỉ
tạo proposed `OnlineState` sau khi toàn batch thành công. Snapshot travel đầu tiên phải
khớp manifest; snapshot sau tăng version đúng một.

`EventReductionCoordinator` tách:

- `CommittedState`: đã được upstream ACK;
- `PendingState`: reducer/policy đã đề xuất nhưng chưa commit.

`StageDecisionState` chỉ cho policy đổi core request/vehicle plan trong cùng epoch;
identity, cursor, travel snapshot và time phải giữ nguyên. `ApplyDecisionAcknowledgement`
mới chuyển pending sang committed.

## PhysicalPlanValidator kiểm gì?

Validator tự đi tuyến từ vị trí xe:

- node position đi thẳng; edge progress lấy phần còn lại bằng phép chia ceil;
- candidate không đổi exact frozen prefix;
- no-op giữ plan version, route đổi tăng đúng một;
- từng directed arc phải tồn tại, không tự thêm reverse/Euclidean fallback;
- pickup đúng origin, không trùng, không sau latest; đến sớm thì chờ earliest;
- load bắt đầu phải bằng tổng party size onboard; mọi pickup/drop giữ `0..capacity`;
- drop phải sau pickup/onboard, đúng destination và không quá max ride time;
- accepted/onboard incumbent phải còn đúng pickup/drop cần thiết;
- accepted incumbent không được chuyển sang xe khác.

Đây là validator độc lập với candidate generator. Candidate bị filter sớm vẫn phải
qua lại validator; một lỗi trong heuristic không thể tự hợp thức hóa route.

## B1 và exact-small oracle

Generator chèn pickup/drop vào mutable suffix theo stable order và luôn giữ no-op.
B1 chọn lexicographic: nhiều accepted mới hơn, integer route cost thấp hơn, rồi stable
candidate ID. Trong bound nhỏ, test oracle tự enumerate route/fleet combination độc
lập để đo generator gap và selection gap. Ngoài bound, code chỉ claim bounded search,
không claim global optimum.
