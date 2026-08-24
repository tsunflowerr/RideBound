# WP13 evidence retention và successor policy v1

Trạng thái: `Accepted`  
Owner: `RB-WP13-013`  
Áp dụng cho evidence hậu H6 từ WP13 trở đi

## 1. Raw evidence retention

H6 và E1 raw roots là immutable evidence authorities. WP13 closure không move,
delete, recompress, deduplicate hay rewrite bất kỳ file nào. E1 được giữ nguyên tại
hai frozen roots đã khai báo trong freeze receipt; authority là exact per-file
length/SHA inventory, không phải filesystem timestamp.

E1 retention decision:

- disposition: `retainVerifiedRawAndFreeze`;
- verified size: 5.516.098.710 byte;
- arm runs: 80;
- solver decisions/retained portfolios: 44.156/44.156;
- repository inventory SHA-256:
  `22f4914e9f61163f8e33089a2f24786bcd4bf0b4c50d42a860fbf8916a3f6afb`;
- deletion permitted: `false`;
- archive action in WP13: `notPerformed`.

Một archive/migration tương lai chỉ được thực hiện bằng ticket riêng, verify byte-exact
source và destination inventories trước cutover, giữ ít nhất một verified copy, phát
signed/canonical move receipt và không xóa source trước independent verification.

## 2. Derived output transaction policy

Analyzer mới hoặc successor của analyzer lịch sử phải:

1. reject output path nằm trong hoặc alias về raw H6/E1 roots;
2. đọc và verify toàn bộ input trước khi tạo success artifact;
3. serialize canonical UTF-8 JSON với LF và không BOM;
4. tạo success output bằng exclusive create; existing target là hard failure;
5. không để partial success artifact khi validation fail;
6. bind exact source/schema/input length và SHA-256;
7. ghi `supersedes`/`supersededBy` cùng reason khi thay artifact, không overwrite;
8. giữ failure logs ngoài raw roots và không gọi chúng là success receipt.

Các pre-E1 analyzer hiện hữu không bị sửa hồi tố: raw-root protection và exact hashes
đã đủ giữ historical authority. Policy này là required contract cho successor/future
outputs, không rewrite provenance cũ.

## 3. Versioned successor verifier

Frozen E1 verifier SHA-256
`89a9e9a797e7d7f004490bff3bc37da14cd792c14ff60513873ed51b96c06a17`
vẫn là historical execution authority và không được mutate.

Evidence execution mới sau WP13 phải dùng verifier version mới. Successor tối thiểu
phải giữ toàn bộ historical checks và thêm:

- canonical integer semantics: boolean không phải integer;
- optional field type/absence distinction, không dùng `null` thay omission;
- identifier giới hạn chính xác 1–128 UTF-8 byte, không chỉ character count;
- strict unknown-field rejection bằng contract schema;
- regression vectors cho wrong-type/overlength `repairedIncumbentRequestId` và
  boolean objective `levelIndex`;
- source/schema/test hash trong freeze receipt trước execution.

Successor không được tự đổi protocol/policy semantics. Minor verifier upgrade phải có
typed compatibility note; semantic change yêu cầu new evidence namespace và freeze.

## 4. WP14 resource guard

Việc E1 chiếm 5,516 GB chỉ là instrumentation cost observation. WP14 không được mặc
định nhân bản toàn E1 matrix. Trước execution, refinement phải:

- dùng development cells/namespace mới, loại H6 confirmatory panels khỏi selection;
- lập factor matrix và analytical denominator trước outcome;
- chạy schema-only và tiny dry-run trước;
- estimate bytes/decision và đặt declared disk envelope;
- chỉ retain field cần cho ablation/Pareto question;
- có cancellation receipt khi disk/time envelope bị vượt.

Policy này không chọn budget, lock, horizon, objective hoặc policy v2 candidate.
