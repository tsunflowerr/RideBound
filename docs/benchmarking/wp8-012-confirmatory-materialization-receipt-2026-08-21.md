# Confirmatory materialization receipt — RB-WP8-012

Không Runner/policy nào được gọi trong ticket này; chỉ source normalization và
hashing. Grid v1 target 128 dừng fail-closed ở cell thứ 13 do node-cap coverage.
Không loại cell riêng. Amendment pre-outcome chọn target đồng nhất tối đa 108 và
grid v2 materialize đủ 20/20.

| Receipt | SHA-256 |
|---|---|
| preregistration `H0` | `c653c3ceeadbd1a2dd494213cdde0498fd8c464e73071dec00ae521ae01b1fc2` |
| amendment | `184bf923160f940d29e5874d1934036c9c41a51b385c32e68cffd5bf555fddf9` |
| grid v2 | `523486722805f1af38a2eacdc427d2cb4991f9015a9559eee431fdd30b587726` |
| derivative tree | `623ebfae905355007aa9bedca4687646973d8a9c9da8b25d67e5409b02abc943` |
| execution plan | `249e29778d048b8edf32ae3e1011391442c384b46c8cb8cbef9080c666220277` |
| freeze receipt `H2` | `97af95cfba463a4611395e9a2b59ea8d33d44c2f887c712319eeda34036d3049` |
| analysis-integrity amendment | `db1731e6a2a9d7cebd630d880bfbba4c1a15c0e54f1f581f3ccdd5ad826f1d70` |
| superseding freeze receipt `H3` | `d028eae4c3e9c518fb0f88a108ce30084cfa216f60531c079c92f5ea0fcdd14e` |
| Runner-repin amendment | `d1d9465e5a2f509a9892ab3d9b878246d4262afaccfa4d745f68c660cdeac90e` |
| current freeze receipt `H4` | `2f7e6bf36c16784e06cb3266f9764f3103f2de6fc931f3c8e023bdc1a81a32dd` |

Normalizer chạy lần hai trên v2 trả `reusedExactDerivative=true` cho 20/20.
Mỗi cell: 108 requests, 8 xe, 96 node, 9.120 directed arcs, conservation
`input = eligible + exclusions` và `selected=108`. Selection/pseudonymization
label đều dẫn xuất từ `H0`, không có key do người nghiên cứu chọn sau freeze.

Derivative v1 một phần được giữ như failure evidence, không dùng trong WP9.
Derivative v2 và driver nằm dưới `benchmarks/fixtures/.../wp9-confirmatory-fixed-panel-v2`
và `benchmarks/scenarios/wp9-confirmatory`.

`H2`/`H3` được giữ byte-nguyên làm lịch sử. Trước outcome confirmatory, kiểm receipt
Layer-1 phát hiện pin Runner của H3 cũ hơn build đã review. Amendment `WP8-011c` và
`H4` repin DLL hiện hành `4c297a2c…bd2a8`, bind thêm toàn publish-tree seal
`29a8195b…4589`; verifier riêng recompute 25 file/Runner hash và ba tree seal.
Panel/scenario/config/endpoint/margin không đổi.
